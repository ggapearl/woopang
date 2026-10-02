import Foundation

/// PC 효딩쓰가 보내는 JSON 을 모양을 미리 정하지 않고 읽는다 (사건마다 필드가 다르다).
enum JSON: Decodable, Equatable {
    case string(String)
    case number(Double)
    case bool(Bool)
    case object([String: JSON])
    case array([JSON])
    case null

    init(from decoder: Decoder) throws {
        let c = try decoder.singleValueContainer()
        if c.decodeNil() {
            self = .null
        } else if let v = try? c.decode(Bool.self) {
            self = .bool(v)
        } else if let v = try? c.decode(Double.self) {
            self = .number(v)
        } else if let v = try? c.decode(String.self) {
            self = .string(v)
        } else if let v = try? c.decode([JSON].self) {
            self = .array(v)
        } else {
            self = .object(try c.decode([String: JSON].self))
        }
    }

    static func parse(_ data: Data) throws -> JSON {
        try JSONDecoder().decode(JSON.self, from: data)
    }

    subscript(key: String) -> JSON? {
        if case .object(let o) = self { return o[key] }
        return nil
    }

    var string: String? {
        if case .string(let s) = self { return s }
        return nil
    }

    var double: Double? {
        if case .number(let n) = self { return n }
        return nil
    }

    var int: Int? { double.map { Int($0) } }

    var bool: Bool? {
        if case .bool(let b) = self { return b }
        return nil
    }

    var array: [JSON]? {
        if case .array(let a) = self { return a }
        return nil
    }
}
