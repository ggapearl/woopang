import BackgroundTasks
import Foundation
import Photos
import UIKit

/// 폰 사진 자동 백업 + 폰 사진첩 정리 (2.4, 2026-11-21 대표님 「구글 포토처럼 · 요금제 무제한이라 LTE·Wi-Fi 상관없이 · 백업된 건 폰에서 정리」).
///
/// 화면(웹 app.js)은 `DeskNative` 로 이 클래스를 부른다 — 이름·모양은 app.js 의 「폰 사진 자동 백업」 주석 / `PHOTO_BACKUP.md` 와 같다.
///  - **기준 시각(`since`) 이후**에 이 폰에 생긴 사진·영상만 올리고, 그 뒤에 찍는 것도 계속 올린다. 처음 켤 때 기준은 **2026-11-27 0시**
///    (대표님 11-27 「오늘 0시부터 · 내일 사진도 계속」 — 켠 시각이 아니다). 그 전의 옛 사진은 건드리지 않는다.
///    껐다가 다시 켜면 기준이 그때 시각으로 새로 찍힌다(꺼 둔 동안 찍은 건 올리지 않는다).
///  - 올리는 곳은 PC 효딩쓰 창구 `photos/up` — 6MB 이하 조각으로, 끊기면 서버가 가진 길이(`have`)부터 이어 올린다.
///    LTE·Wi-Fi 구분 없음(`allowsCellularAccess` 등 모두 true).
///  - 앱이 열려 있을 때·사진첩이 바뀔 때·앱을 떠난 직후 얼마 동안·시스템이 주는 백그라운드 시간(`BGProcessingTask`)에 올린다.
///    iOS 는 꺼진 앱을 계속 돌릴 수 없다 — 이게 한계다.
///  - **정리**: 올라간 것 중 폰 사진첩에 아직 남은 것을 서버(`photos/up/verify`)에 다시 물어, 서버에 있다고 한 것만
///    `PHAssetChangeRequest.deleteAssets` 로 지운다(iOS 가 확인 창을 한 번 띄운다 · 「최근 삭제된 항목」에 30일).
@MainActor
final class PhotoBackup: NSObject, PHPhotoLibraryChangeObserver {
    static let shared = PhotoBackup()
    static let bgTaskId = "com.que.hyodingsseu.photobackup"      // Info.plist BGTaskSchedulerPermittedIdentifiers 와 같아야 한다
    private static let chunkSize = 5 * 1024 * 1024                // 서버 조각 한도(6MB) 아래

    /// 처음 켤 때의 기준 시각 — 2026-11-27 0시(폰의 현재 시간대). 켠 시각이 아니라 이 날 0시라서, 이 날 찍은 사진부터 올라간다.
    private static var firstSince: Double {
        var c = DateComponents()
        c.year = 2026
        c.month = 11
        c.day = 27
        return (Calendar.current.date(from: c) ?? Date()).timeIntervalSince1970
    }

    /// 상태가 바뀌면 화면에 알린다 — DeskNative 가 'backup' 사건으로 넘긴다
    var onChange: (([String: Any]) -> Void)?

    // MARK: 저장하는 것 (앱 지원 폴더 JSON — 앱을 껐다 켜도, 백그라운드에서 깨어나도 같은 값)

    private struct Store: Codable {
        var enabled = false
        var since: Double = 0                 // 기준 시각(초) — 처음 켜면 2026-11-27 0시, 다시 켜면 그때
        var base = ""                         // 예: https://woopang.com/AI/api/desk
        var token = ""                        // 기기 열쇠
        var done: [String: Int64] = [:]       // 올린 사진 번호(localIdentifier) → 올린 크기(바이트)
    }

    private struct UpProgress {
        var lid: String
        var name: String
        var sent: Int64
        var total: Int64
    }

    private enum Fail: Error {
        case auth                             // 열쇠가 없거나 틀림(401·403)
        case offline(String)                  // 인터넷·PC·서버 문제 — 조금 뒤 다시
        case skip(String)                     // 이 사진만 올릴 수 없음 — 건너뛴다
    }

    private struct HTTPResult {
        let status: Int
        let json: [String: Any]
    }

    private var store = Store()
    private var pendingIds: [String] = []     // 아직 안 올린 사진 번호(새것부터)
    private var failed = Set<String>()        // 이번에 올리지 못해 건너뛴 것(앱을 열 때·「지금 올리기」에 다시 시도)
    private var uploading: UpProgress?
    private var lastError = ""
    private var runTask: Task<Void, Never>?
    private var dirty = false                 // 올리는 중에 사진첩이 또 바뀌었다 — 끝나고 한 번 더
    private var bgId: UIBackgroundTaskIdentifier = .invalid
    private var observing = false
    private var started = false
    private var lastPublish = Date.distantPast

    private let session: URLSession = {
        let c = URLSessionConfiguration.default
        c.allowsCellularAccess = true                  // LTE 도 올린다 (대표님: 요금제 무제한)
        c.allowsExpensiveNetworkAccess = true
        c.allowsConstrainedNetworkAccess = true
        c.waitsForConnectivity = false
        c.timeoutIntervalForRequest = 90
        c.timeoutIntervalForResource = 900
        return URLSession(configuration: c)
    }()

    private let fileURL: URL = {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("PhotoBackup", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir.appendingPathComponent("state.json")
    }()

    // MARK: 시작 · 생명주기

    /// 앱이 뜰 때 한 번 — AppDelegate.didFinishLaunching (BGTask 등록은 앱 시작이 끝나기 전에 해야 한다)
    static func registerBackgroundTask() {
        guard let ids = Bundle.main.object(forInfoDictionaryKey: "BGTaskSchedulerPermittedIdentifiers") as? [String],
              ids.contains(bgTaskId) else { return }     // Info.plist 에 없으면 등록하지 않는다(등록하면 앱이 죽는다)
        BGTaskScheduler.shared.register(forTaskWithIdentifier: bgTaskId, using: nil) { task in
            Task { @MainActor in PhotoBackup.shared.handleBGTask(task) }
        }
    }

    /// 화면이 처음 만들어질 때 — 저장된 값을 읽고, 켜져 있으면 바로 이어서 한다
    func start() {
        guard !started else { return }
        started = true
        load()
        NotificationCenter.default.addObserver(self, selector: #selector(didEnterBackground),
                                               name: UIApplication.didEnterBackgroundNotification, object: nil)
        if store.enabled { activate() }
    }

    /// 앱이 앞으로 나왔을 때(WebShell) — 못 올렸던 것도 다시 시도
    func appBecameActive() {
        guard store.enabled else { return }
        failed.removeAll()
        activate()
    }

    @objc private func didEnterBackground() {
        guard store.enabled else { return }
        scheduleBGTask()
        beginBackground()
    }

    private func beginBackground() {
        guard runTask != nil, bgId == .invalid else { return }
        bgId = UIApplication.shared.beginBackgroundTask(withName: "photo-backup") { [weak self] in
            Task { @MainActor in
                self?.runTask?.cancel()
                self?.endBackground()
            }
        }
    }

    private func endBackground() {
        if bgId != .invalid {
            UIApplication.shared.endBackgroundTask(bgId)
            bgId = .invalid
        }
    }

    private func scheduleBGTask() {
        guard store.enabled else { return }
        let req = BGProcessingTaskRequest(identifier: Self.bgTaskId)
        req.requiresNetworkConnectivity = true
        req.requiresExternalPower = false
        req.earliestBeginDate = Date(timeIntervalSinceNow: 15 * 60)
        try? BGTaskScheduler.shared.submit(req)
    }

    private func handleBGTask(_ task: BGTask) {
        scheduleBGTask()                                  // 다음 번 예약
        guard store.enabled else {
            task.setTaskCompleted(success: true)
            return
        }
        task.expirationHandler = { [weak self] in
            Task { @MainActor in self?.runTask?.cancel() }
        }
        activate()
        Task { @MainActor in
            if let t = self.runTask { await t.value }
            task.setTaskCompleted(success: true)
        }
    }

    private func activate() {
        guard store.enabled, Self.isAuthorized else { return }
        observe(true)
        refreshPending()
        publish(force: true)
        run()
    }

    private func observe(_ on: Bool) {
        if on, !observing {
            PHPhotoLibrary.shared().register(self)
            observing = true
        } else if !on, observing {
            PHPhotoLibrary.shared().unregisterChangeObserver(self)
            observing = false
        }
    }

    nonisolated func photoLibraryDidChange(_ changeInstance: PHChange) {
        Task { @MainActor in self.libraryChanged() }
    }

    private func libraryChanged() {
        guard store.enabled else { return }
        refreshPending()
        publish(force: true)
        run()
    }

    // MARK: 화면이 부르는 것

    func configure(base: String?, token: String?) {
        if let b = base?.trimmingCharacters(in: .whitespaces), !b.isEmpty {
            var s = b
            while s.hasSuffix("/") { s.removeLast() }
            store.base = s
        }
        if let t = token { store.token = t }
        save()
    }

    func status() -> [String: Any] {
        var err = lastError
        if err.isEmpty, !failed.isEmpty { err = "올리지 못한 사진·영상 \(failed.count)개 — 「지금 올리기」로 다시 시도해요" }
        var s: [String: Any] = [
            "enabled": store.enabled,
            "since": store.since,
            "auth": Self.authString(),
            "done": store.done.count,
            "pending": pendingIds.count,
            "wifiOnly": false,
            "error": err,
        ]
        if let u = uploading {
            s["uploading"] = ["lid": u.lid, "name": u.name, "sent": u.sent, "total": u.total] as [String: Any]
        } else {
            s["uploading"] = NSNull()
        }
        return s
    }

    func enable() async -> [String: Any] {
        var st = PHPhotoLibrary.authorizationStatus(for: .readWrite)
        if st == .notDetermined { st = await PHPhotoLibrary.requestAuthorization(for: .readWrite) }
        guard st == .authorized || st == .limited else {
            store.enabled = false
            save()
            lastError = ""
            return status()
        }
        guard !store.token.isEmpty, endpoint("photos/up/state") != nil else {
            store.enabled = false
            save()
            lastError = "PC 효딩쓰와 연결된 뒤에 켤 수 있어요"
            return status()
        }
        store.enabled = true
        // 처음 켜면 2026-11-27 0시부터(오늘·내일 사진 모두), 껐다 다시 켜면 그때부터 — 이후에 생긴 것을 계속 올린다
        store.since = store.since > 0 ? Date().timeIntervalSince1970 : Self.firstSince
        failed.removeAll()
        lastError = ""
        save()
        observe(true)
        refreshPending()
        publish(force: true)
        scheduleBGTask()
        run()
        return status()
    }

    func disable() -> [String: Any] {
        store.enabled = false
        save()
        runTask?.cancel()
        observe(false)
        pendingIds = []
        uploading = nil
        lastError = ""
        BGTaskScheduler.shared.cancel(taskRequestWithIdentifier: Self.bgTaskId)
        publish(force: true)
        return status()
    }

    /// 올리기 시작(이미 하는 중이면 한 번 더 훑도록 표시만). manual 이면 건너뛴 것도 다시.
    func run(manual: Bool = false) {
        guard store.enabled, Self.isAuthorized else { return }
        if manual {
            failed.removeAll()
            lastError = ""
        }
        guard runTask == nil else {
            dirty = true
            return
        }
        runTask = Task { [weak self] in
            guard let self else { return }
            repeat {
                self.dirty = false
                await self.loop()
            } while self.dirty && !Task.isCancelled && self.store.enabled
            self.runTask = nil
            self.endBackground()
            self.publish(force: true)
        }
        if UIApplication.shared.applicationState != .active { beginBackground() }
    }

    /// 켠 뒤의 사진·영상 목록(새것부터). before = 이 시각(초) 전
    func list(before: Double, limit: Int) -> [String: Any] {
        guard store.since > 0, Self.isAuthorized else { return ["items": [] as [Any], "more": false] }
        let lim = max(1, min(limit, 200))
        let r = fetchNew(limit: lim + 1, before: before > 0 ? Date(timeIntervalSince1970: before) : nil)
        var items: [[String: Any]] = []
        let n = min(r.count, lim)
        for i in 0..<n {
            let a = r.object(at: i)
            let name = PHAssetResource.assetResources(for: a).first?.originalFilename ?? ""
            items.append([
                "lid": a.localIdentifier,
                "kind": a.mediaType == .video ? "video" : "image",
                "ts": (a.creationDate ?? Date()).timeIntervalSince1970,
                "dur": a.mediaType == .video ? a.duration : 0,
                "name": name,
                "up": store.done[a.localIdentifier] != nil,
            ])
        }
        return ["items": items, "more": r.count > lim]
    }

    /// 작은 그림 — 'data:image/jpeg;base64,…'
    func thumb(lid: String, size: Int) async throws -> [String: Any] {
        guard let asset = PHAsset.fetchAssets(withLocalIdentifiers: [lid], options: nil).firstObject else {
            throw Fail.skip("사진을 찾지 못했어요")
        }
        let px = CGFloat(max(60, min(size, 600)))
        let opts = PHImageRequestOptions()
        opts.deliveryMode = .highQualityFormat
        opts.resizeMode = .fast
        opts.isNetworkAccessAllowed = true                  // iCloud 에만 있는 것도
        opts.isSynchronous = false
        let image: UIImage = try await withCheckedThrowingContinuation { (c: CheckedContinuation<UIImage, Error>) in
            var finished = false
            PHImageManager.default().requestImage(for: asset, targetSize: CGSize(width: px, height: px),
                                                  contentMode: .aspectFill, options: opts) { img, info in
                if finished { return }
                guard let img else {
                    finished = true
                    c.resume(throwing: Fail.skip("그림을 만들지 못했어요"))
                    return
                }
                if (info?[PHImageResultIsDegradedKey] as? Bool) == true { return }       // 거친 그림은 건너뛰고 좋은 그림을 기다린다
                finished = true
                c.resume(returning: img)
            }
        }
        guard let jpg = image.jpegData(compressionQuality: 0.72) else { throw Fail.skip("그림을 만들지 못했어요") }
        return ["data": "data:image/jpeg;base64," + jpg.base64EncodedString()]
    }

    // MARK: 정리 — 올라간 것만 폰에서 지운다

    /// 올라갔고 폰 사진첩에 아직 남아 있는 것
    private func uploadedPresent() -> [PHAsset] {
        let ids = Array(store.done.keys)
        var out: [PHAsset] = []
        var i = 0
        while i < ids.count {
            let batch = Array(ids[i..<min(i + 500, ids.count)])
            PHAsset.fetchAssets(withLocalIdentifiers: batch, options: nil).enumerateObjects { a, _, _ in out.append(a) }
            i += 500
        }
        return out
    }

    func cleanupPlan() -> [String: Any] {
        var vn = 0, inn = 0
        var vb: Int64 = 0, ib: Int64 = 0
        for a in uploadedPresent() {
            let b = store.done[a.localIdentifier] ?? 0
            if a.mediaType == .video {
                vn += 1
                vb += b
            } else if a.mediaType == .image {
                inn += 1
                ib += b
            }
        }
        return ["video": ["n": vn, "bytes": vb] as [String: Any], "image": ["n": inn, "bytes": ib] as [String: Any]]
    }

    /// scope: "video" = 동영상만 · "all" = 사진·동영상 모두. 지우기 직전에 서버에 다시 물어 「서버에 있다」고 한 것만 지운다.
    func cleanup(scope: String) async throws -> [String: Any] {
        func result(_ v: Int, _ i: Int, _ pending: Int, _ skipped: Int, _ cancelled: Bool) -> [String: Any] {
            ["video": v, "image": i, "pending": pending, "skipped": skipped, "cancelled": cancelled]
        }
        guard Self.isAuthorized else { throw Fail.skip("설정 › 효딩쓰 › 사진 에서 사진 접근을 허락해 주세요") }
        let all = uploadedPresent()
        let targets = scope == "video" ? all.filter { $0.mediaType == .video } : all.filter { $0.mediaType == .video || $0.mediaType == .image }
        guard !targets.isEmpty else { return result(0, 0, 0, 0, false) }

        var ok = Set<String>()
        var pending = 0, skipped = 0
        let ids = targets.map { $0.localIdentifier }
        var i = 0
        while i < ids.count {
            let batch = Array(ids[i..<min(i + 150, ids.count)])        // 서버가 한 번에 150개까지
            i += 150
            guard let url = endpoint("photos/up/verify"),
                  let body = try? JSONSerialization.data(withJSONObject: ["ids": batch]) else { throw Fail.skip("서버 주소가 이상해요") }
            let r: HTTPResult
            do {
                r = try await send(url, body: body, contentType: "application/json", timeout: 60)
            } catch {
                throw Fail.offline("서버에 확인하지 못했어요 — 인터넷을 확인하고 다시 눌러 주세요")
            }
            if r.status == 401 || r.status == 403 { throw Fail.auth }
            guard r.status == 200 else { throw Fail.offline("서버 확인이 실패했어요 (\(r.status))") }
            for l in (r.json["ok"] as? [String]) ?? [] { ok.insert(l) }
            pending += (r.json["pending"] as? [Any])?.count ?? 0
            skipped += (r.json["missing"] as? [Any])?.count ?? 0
        }
        let victims = targets.filter { ok.contains($0.localIdentifier) }
        guard !victims.isEmpty else { return result(0, 0, pending, skipped, false) }
        let nv = victims.filter { $0.mediaType == .video }.count
        do {
            try await PHPhotoLibrary.shared().performChanges {
                PHAssetChangeRequest.deleteAssets(victims as NSArray)       // iOS 가 「N개를 삭제할까요」 확인 창을 띄운다
            }
        } catch {
            let ns = error as NSError
            if ns.domain == PHPhotosErrorDomain, ns.code == PHPhotosError.Code.userCancelled.rawValue {
                return result(0, 0, pending, skipped, true)
            }
            throw Fail.skip("폰 사진첩에서 지우지 못했어요")
        }
        publish(force: true)
        return result(nv, victims.count - nv, pending, skipped, false)
    }

    static func message(_ error: Error) -> String {
        if let f = error as? Fail {
            switch f {
            case .auth: return "PC 와 연결이 끊겼어요 — 효딩쓰를 다시 연결해 주세요"
            case .offline(let m), .skip(let m): return m
            }
        }
        return "처리하지 못했어요"
    }

    // MARK: 올리기

    private static var isAuthorized: Bool {
        let s = PHPhotoLibrary.authorizationStatus(for: .readWrite)
        return s == .authorized || s == .limited
    }

    private static func authString() -> String {
        switch PHPhotoLibrary.authorizationStatus(for: .readWrite) {
        case .authorized: return "granted"
        case .limited: return "limited"
        case .denied, .restricted: return "denied"
        default: return "undetermined"
        }
    }

    /// 켠 시각 이후의 사진·영상 (before 가 있으면 그 시각 전). 기본은 새것부터.
    private func fetchNew(limit: Int = 0, before: Date? = nil) -> PHFetchResult<PHAsset> {
        var preds: [NSPredicate] = [
            NSPredicate(format: "creationDate > %@", Date(timeIntervalSince1970: store.since) as NSDate),
            NSPredicate(format: "mediaType == %ld || mediaType == %ld", PHAssetMediaType.image.rawValue, PHAssetMediaType.video.rawValue),
        ]
        if let b = before { preds.append(NSPredicate(format: "creationDate < %@", b as NSDate)) }
        let o = PHFetchOptions()
        o.predicate = NSCompoundPredicate(andPredicateWithSubpredicates: preds)
        o.sortDescriptors = [NSSortDescriptor(key: "creationDate", ascending: false)]
        if limit > 0 { o.fetchLimit = limit }
        return PHAsset.fetchAssets(with: o)
    }

    private func refreshPending() {
        guard store.enabled, Self.isAuthorized, store.since > 0 else {
            pendingIds = []
            return
        }
        let done = store.done
        var ids: [String] = []
        fetchNew().enumerateObjects { a, _, _ in
            if done[a.localIdentifier] == nil { ids.append(a.localIdentifier) }
        }
        pendingIds = ids
    }

    private func loop() async {
        var offlineTries = 0
        await syncKnown()
        while !Task.isCancelled, store.enabled {
            refreshPending()
            guard let lid = pendingIds.first(where: { !failed.contains($0) }) else { break }
            guard let asset = PHAsset.fetchAssets(withLocalIdentifiers: [lid], options: nil).firstObject else {
                failed.insert(lid)                      // 그 사이 폰에서 지워졌다
                continue
            }
            do {
                try await upload(asset)
                offlineTries = 0
                lastError = ""
            } catch Fail.auth {
                lastError = Self.message(Fail.auth)
                break
            } catch Fail.offline(let why) {
                lastError = why
                uploading = nil
                publish(force: true)
                offlineTries += 1
                if offlineTries > 5 { break }
                try? await Task.sleep(nanoseconds: UInt64(offlineTries) * 30_000_000_000)      // 30초씩 늘려 가며 다시
                continue
            } catch Fail.skip(_) {
                failed.insert(lid)
            } catch {
                break                                   // 취소(앱이 백그라운드 시간을 다 씀)
            }
            uploading = nil
            publish(force: true)
        }
        uploading = nil
    }

    /// 앱을 지웠다 다시 깔았거나 사진첩을 다시 훑을 때 — 서버가 이미 가진 것은 다시 올리지 않는다
    private func syncKnown() async {
        refreshPending()
        let ids = pendingIds
        var i = 0
        while i < ids.count {
            let batch = Array(ids[i..<min(i + 400, ids.count)])
            i += 400
            guard let url = endpoint("photos/up/check"),
                  let body = try? JSONSerialization.data(withJSONObject: ["ids": batch]),
                  let r = try? await send(url, body: body, contentType: "application/json", timeout: 60),
                  r.status == 200,
                  let got = r.json["done"] as? [String] else { return }
            for l in got where store.done[l] == nil { store.done[l] = 0 }
            save()
        }
        refreshPending()
    }

    private func upload(_ asset: PHAsset) async throws {
        let lid = asset.localIdentifier
        let isVideo = asset.mediaType == .video
        let resources = PHAssetResource.assetResources(for: asset)
        let wanted: [PHAssetResourceType] = isVideo ? [.video, .fullSizeVideo] : [.photo, .fullSizePhoto, .alternatePhoto]
        guard let res = wanted.compactMap({ t in resources.first(where: { $0.type == t }) }).first else {
            throw Fail.skip("원본 파일을 찾지 못했어요")
        }
        let name = res.originalFilename
        uploading = UpProgress(lid: lid, name: name, sent: 0, total: 0)
        publish()

        // 원본을 임시 파일로 꺼낸다(iCloud 에 있으면 받아서) — 영상 몇 GB 도 메모리에 올리지 않는다
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("photo-backup", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let ext = (name as NSString).pathExtension
        let tmp = dir.appendingPathComponent(UUID().uuidString + (ext.isEmpty ? "" : "." + ext))
        defer { try? FileManager.default.removeItem(at: tmp) }
        do {
            try await export(res, to: tmp)
        } catch {
            throw Fail.skip("사진 원본을 가져오지 못했어요")
        }
        let attrs = try? FileManager.default.attributesOfItem(atPath: tmp.path)
        let total = (attrs?[.size] as? NSNumber)?.int64Value ?? 0
        guard total > 0 else { throw Fail.skip("빈 파일이에요") }
        uploading = UpProgress(lid: lid, name: name, sent: 0, total: total)
        publish()

        let ts = Int((asset.creationDate ?? Date()).timeIntervalSince1970)
        let mod = Int((asset.modificationDate ?? asset.creationDate ?? Date()).timeIntervalSince1970)
        let kind = isVideo ? "video" : "image"
        let handle: FileHandle
        do { handle = try FileHandle(forReadingFrom: tmp) } catch { throw Fail.skip("파일을 읽지 못했어요") }
        defer { try? handle.close() }

        func post(_ data: Data, off: Int64) async throws -> HTTPResult {
            guard let url = endpoint("photos/up", query: [("lid", lid), ("off", String(off)), ("total", String(total)),
                                                         ("name", name), ("ts", String(ts)), ("mod", String(mod)), ("kind", kind)]) else {
                throw Fail.skip("서버 주소가 이상해요")
            }
            var last: Error?
            for attempt in 0..<3 {
                do {
                    return try await send(url, body: data, contentType: "application/octet-stream", timeout: 90)
                } catch {
                    if (error as? URLError)?.code == .cancelled { throw CancellationError() }
                    last = error
                    try await Task.sleep(nanoseconds: UInt64(attempt + 1) * 2_000_000_000)
                }
            }
            _ = last
            throw Fail.offline("인터넷이 불안정해요 — 연결되면 다시 올려요")
        }

        func check(_ r: HTTPResult) throws {
            if r.status == 401 || r.status == 403 { throw Fail.auth }
        }

        // 이어받기 확인 — off=total 로 빈 조각을 보내 서버가 가진 길이를 묻는다.
        // 이미 있으면 200 done · 조각이 있으면 409 {have} · 아무것도 없으면 409 {have: 0}
        var off: Int64 = 0
        var finished = false
        let probe = try await post(Data(), off: total)
        try check(probe)
        if probe.status == 200, (probe.json["done"] as? Bool) == true {
            finished = true
        } else if probe.status == 409, let have = (probe.json["have"] as? NSNumber)?.int64Value, have > 0, have < total {
            off = have
        }

        var conflicts = 0
        var rounds = 0
        let cap = Int(total / Int64(Self.chunkSize)) + 40
        while !finished {
            try Task.checkCancellation()
            rounds += 1
            if rounds > cap { throw Fail.offline("서버와 조각 위치가 맞지 않아요 — 조금 뒤에 다시 올려요") }
            let n = Int(min(Int64(Self.chunkSize), max(total - off, 0)))
            var data = Data()
            if n > 0 {
                do {
                    try handle.seek(toOffset: UInt64(off))
                    data = try handle.read(upToCount: n) ?? Data()
                } catch { throw Fail.skip("파일을 읽지 못했어요") }
                if data.count != n { throw Fail.skip("파일을 읽지 못했어요") }
            }
            let r = try await post(data, off: off)
            try check(r)
            switch r.status {
            case 200:
                if (r.json["done"] as? Bool) == true {
                    finished = true
                } else if let have = (r.json["have"] as? NSNumber)?.int64Value {
                    off = have
                } else {
                    off += Int64(n)
                }
            case 409:
                conflicts += 1
                if conflicts > 6 { throw Fail.offline("서버와 조각 위치가 계속 어긋나요 — 조금 뒤에 다시 올려요") }
                if let have = (r.json["have"] as? NSNumber)?.int64Value, have <= total { off = have } else { off = 0 }
            case 400, 413:
                throw Fail.skip((r.json["error"] as? String) ?? "서버가 이 파일을 받지 않았어요")
            default:
                throw Fail.offline((r.json["error"] as? String) ?? "PC 가 답하지 않아요 (\(r.status))")
            }
            uploading?.sent = min(off, total)
            publish()
        }
        store.done[lid] = total
        save()
    }

    private func export(_ res: PHAssetResource, to url: URL) async throws {
        let opts = PHAssetResourceRequestOptions()
        opts.isNetworkAccessAllowed = true
        try await withCheckedThrowingContinuation { (c: CheckedContinuation<Void, Error>) in
            PHAssetResourceManager.default().writeData(for: res, toFile: url, options: opts) { err in
                if let err { c.resume(throwing: err) } else { c.resume(returning: ()) }
            }
        }
    }

    // MARK: 서버 통신

    private static let unreserved = CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~")

    private static func enc(_ s: String) -> String {
        s.addingPercentEncoding(withAllowedCharacters: unreserved) ?? ""
    }

    /// 열쇠를 woopang.com 밖으로는 보내지 않는다
    private func endpoint(_ path: String, query: [(String, String)] = []) -> URL? {
        guard !store.base.isEmpty, var comps = URLComponents(string: store.base + "/" + path),
              comps.scheme == "https", let host = comps.host?.lowercased(),
              host == "woopang.com" || host == "www.woopang.com" else { return nil }
        if !query.isEmpty {
            comps.percentEncodedQuery = query.map { Self.enc($0.0) + "=" + Self.enc($0.1) }.joined(separator: "&")
        }
        return comps.url
    }

    private func send(_ url: URL, body: Data, contentType: String, timeout: TimeInterval) async throws -> HTTPResult {
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        req.timeoutInterval = timeout
        req.setValue("Bearer " + store.token, forHTTPHeaderField: "Authorization")
        req.setValue(contentType, forHTTPHeaderField: "Content-Type")
        req.setValue("application/json", forHTTPHeaderField: "Accept")
        let (data, resp) = try await session.upload(for: req, from: body)
        let code = (resp as? HTTPURLResponse)?.statusCode ?? 0
        let json = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] ?? [:]
        return HTTPResult(status: code, json: json)
    }

    // MARK: 저장 · 알림

    private func load() {
        guard let d = try? Data(contentsOf: fileURL), let s = try? JSONDecoder().decode(Store.self, from: d) else { return }
        store = s
    }

    private func save() {
        guard let d = try? JSONEncoder().encode(store) else { return }
        try? d.write(to: fileURL, options: .atomic)
    }

    private func publish(force: Bool = false) {
        let now = Date()
        if !force, now.timeIntervalSince(lastPublish) < 0.4 { return }
        lastPublish = now
        onChange?(status())
    }
}
