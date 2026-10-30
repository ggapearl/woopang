using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 0926 시안 적용. 기존 오브젝트는 지우지 않고 옮기고·다시 칠한다(참조가 끊기지 않게).
    /// 여러 번 돌려도 같은 결과가 나오도록 "찾고 없으면 만든다 → 값을 다시 넣는다" 로만 쓴다.
    /// 캔버스 단위: 기준 1440x3200, iPhone 15 에서 1pt ≈ 3.7 단위.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        public const int Version = 57;  // v57: 10-27 대표님 「정보 수정 화면 우측 상단 '삭제' 제거 (오브젝트 더블터치에 삭제가 이미 있음)」 — 내 장소 수정 카드의 '삭제'(DeletePlace0926)를 지움 · 지우기는 장소 '···' 의 '삭제' 하나로
        // v56: 10-27 대표님 「목록에서 '보물찾기' 리스트의 경우 살짝 하이라이트, 반짝임 등이 있어서 좀 구분되서 보이도록」 — 목록의 보물찾기 줄: 은은한 금빛 바탕과 가는 금빛 테두리 · 한 주기(3.2초)에 한 번 상자 옆 별이 반짝이고 빛 띠가 줄을 지나감 (R0926PlaceRows 테두리·빛 띠·별 그림 연결)
        // v55: 10-27 대표님 「'전체' 탭을 눌렀을 때 발생하는 각 이모지가 '목록' 에서도 동그라미가 아닌 이모지가 발생하도록」 「업로드하는 분류에 있어서도 마찬가지로 '카테고리' 이모지가 해당 분류 앞에」 「목록탭에 있어서 각 '필터' 에 있어서도 각각의 이모지가 … 동그라미가 아닌 이모지를 글자 앞에」 — 분류 그림 한 표(r0926_c_*)로 목록 줄(분류 · 공공데이터 분류 · 지하철/기차/버스/공항 · 관광 · 3D — 분류 없는 장소만 흰 점) · 필터 칩 여덟 개 모두(전체 네 칸 · 애견동반 발 · 공공데이터 청사 · 축제 / 보물 폭죽 · 지하철 · 버스 · 3D 상자 · 사람) · 장소 추가 / 3D 모델 / 정보 수정의 분류 칩(기타 = 흰 핀)
        // v54: 10-27 대표님 「축제 칩 동작 변경 — 다른 칩을 끄지 않고, 기존 목록 맨 위에 축제가 추가」 「'축제' 탭 → '축제 / 보물'」 — 칩 이름 '축제 / 보물'
        // v53: 10-27 대표님 「안드로이드의 경우 '사진설정' 을 하려고 해도 '사진첩' '갤러리' 등으로 안내하지 않고 '내 파일' 로 안내 … 아예 웹으로 이동하지 않고 앱 내에서 수정할 수 있으면」 — 내 프로필 '프로필 편집'을 웹 대신 앱 안 시트로(FullProfilePanel/ProfileEdit0926 · 사진은 사진첩 → 자르기('프로필 사진') → 512 정사각형 · 아이디·소개·전화번호·SNS 는 키보드 위 입력줄 · 비밀번호·계정 삭제만 웹) · 자르기 화면 제목을 여는 쪽이 정함
        // v51: 10-29 지도 — 친구 아래 근처 보물찾기 목록(줄 그림·제목 줄) · 내 위치 방향 쉐브론
        // v50: 10-25 대표님 「장소나 3D모델 추가할 때 '분류' 에 있어서 체크하지 않고 올려도 되도록 … 그냥 흰색으로 … 각 필터링 분류의 색에 맞게 적절힌 인디케이터 색이 반영」 — 분류를 안 골라도 올라감(분류 '' · 색은 안 보냄 → 흰색), 분류 색을 한 팔레트로(칩 점·AR 표시·목록이 같은 색 · 칩 분류는 저장된 색보다 팔레트가 먼저), 정보 수정에서 분류를 끄면 흰색 · 「메세지 입력창에 들어가는 글자 너무 작은데 입력창이랑 글자 좀 키워」 — 대화 입력줄 글자 46→54 · 안내 44→52 · 칸 110→132 · 보내기 116→136 · 줄 150→176(말풍선 칸 아래 194→220 · 늘어나는 최대 250→290), 키보드 위 입력줄은 그대로 · 10-24 「설정탭 하단에 있는 copy 랑 version footer 조금씩만 아래로」 — 버전 칸 56→80 · 맨 아래 여백 48→24 (시험 nocat · chatlook) · v49: 10-24 대표님 「상품 댓글 이나 이런 것에 있어서도 해당 댓글도 '신고' 할 수 있도록 하고 10개정도 신고 들어가면 그냥 삭제 … 본인이 쓴 댓글은 스와이프 통해서 삭제버튼」 · 「'미르네집' 의 경우 내가 댓글을 남겼는데 이게 바로 반영이 안되는데」 — 댓글 창 위 신고 사유 시트(CommentReportSheet0926 · 스팸·광고 / 욕설·비방 / 음란·혐오 / 기타 + 취소 · 끌어내리기 · 뒤로가기 · 바깥 누르기 — 남의 댓글을 왼쪽으로 밀면 주황 '신고', 내 댓글은 빨강 '삭제') · 「'보물찾기' 안내에 있어서 '낚시성' '도박' '유해성' 등에 있어서는 계정 정지 혹은 삭제될 수 있다고 안내문구」 — 보물 확인 카드에 경고 줄(HuntReview0926/Sheet0926/Warn · 숨기기 전에만) · 「앱 내에서 표시되는 건 소수점 4자리까지지만 실제 저장되고 작동하는 로직은 소수점 6자리까지」 — 위치는 GeoHere(Geospatial 우선) 하나로 읽어 double 그대로 6자리 저장, 화면은 4자리, 내려받은 장소 좌표도 double 로 AR 앵커까지 (장면 바뀜 없음 · 시험 coords). v48: 10-26 대표님 「보물찾기 '등록' 하기 전에 한번 검토해서 올릴 수 있도록 … '카드'모달 … '공유하기' 버튼 … '위치공개' 는 없애얄 것 같아」 — 숨기기 전 확인 카드(캔버스 맨 위 층 HuntReview0926 · 고치기/숨기기 → 숨긴 뒤 참가 코드·확인/공유하기) · 상세에 공유하기 · 상품 칸을 제목 아래로 · 위치 공개 칸 삭제 · 비밀번호 힌트 (선택) · 누구에게 아래 안내. v47: 10-26 대표님 「'정보수정' 에 있어서도 … 최소한 한장의 서브사진이 있도록 … 스와이프를 통해서 사진들을 옮겨서 … 보이는 순서를 조절」 — 추가 사진을 꾹 눌러 끌면 순서가 바뀐다(장소 추가·3D 모델·정보 수정) · 장소 추가·3D 모델도 칸마다 x · x 는 칸 크기를 따라감 · 정보 수정은 사진이 있던 장소면 마지막 한 장을 빼지 않음. v46: 10-26 대표님 「상품 나올 때 적당한 효과음이나 진동은 괜찮은데 '두근두근, 짜잔' 등의 글자표현은 발생하지 않도록 삭제 … '툭툭 두번 누르면 보물이 열려요' 이런 내용은 삭제 … 비슷한 시간에 터치해서 중복으로 보물이 열리는 일이 없도록」 — 보물 찾음 연출의 글자(두근두근… · 무엇이 들어 있을까요?/과연 어떤 보물일까요? · 짜잔!)와 그 글자 칸(Caption)을 지움(진동 · 짜잔의 터치음은 그대로) · AR 보물을 한 번만 누르면 뜨던 '두 번 톡톡 누르면 보물이 열려요' · 보물이 보일 때 알림 아랫줄 '보물상자를 두 번 톡톡 눌러 열어 보세요' · 상세의 '… 두 번 톡톡 눌러 열어요'를 지움 · 같은 보물의 두 번째 '찾음'(거의 같은 때 누름 · 늦게 온 답)이 축하 화면을 다시 열지 않음. v45: 10-26 대표님 「'보물상자' 에 비밀번호를 걸 수 있도록 … 4자로 … 비밀번호를 걸어둔 상자는 반드시 '힌트' … '목록' 이나 '보물상자' 이모지, 아이콘, 3D오브젝트 에서 살짝의 표시」 — 보물찾기 '코드 입력' → '비밀번호'(4자 · 걸면 비밀번호 힌트 필수 · 서버도 같은 규칙 · 글자 수가 다르면 '비밀번호는 4자로') · 비밀번호를 건 보물은 보물찾기 목록 제목 앞 금빛 자물쇠 · 장소 목록과 하늘 '근처 보물찾기' 그림에 자물쇠 배지 · 친구 지도 핀 테 오른쪽 아래 자물쇠 · AR 보물(여덟 모양 모두)에 은빛 자물쇠를 앞에 매닮(숨기기 양식의 모양 미리보기에도) · 비밀번호 없이 열려 하면 자물쇠가 대롱 · 찾으면 짜잔 직전에 딸깍 풀려 떨어진 뒤 열림 · 판정 글('코드' → '비밀번호'). v44: 10-26 대표님 「보물을 찾았을 때 보물상자가 열리면서 … 모달 (상품, 몇번째로 찾았는지 기타 정보값) 카드 느낌으로 … 적당한 애니메이션 … 외부 말고 내부 GPU … 보물상자가 살짝 떨리거나 하는 느낌이 있다가 3-4초 후에 짜잔~」 — 축하 화면을 새로: 찾은 그 AR 보물상자(사진 · 3D 모델을 입은 그대로)가 화면 가운데로 날아와 기기 GPU 로 그린 3D 그대로(투명 판 · R0926HuntReveal) 두근두근 3.2초(심장 박동 진동 · 점점 세지는 떨림 · 뚜껑이 두 번 들썩이며 틈으로 빛 · 마지막엔 잘게 떨며 빨라짐 · 화면을 누르면 바로) → 짜잔(번쩍 · 뚜껑이 확 열림 · 빛줄기 · 금화 · 꽃가루 · 별빛 · 강한 진동) → 상자에서 금박 카드가 솟아 뒤집힘(뒷면 ? 메달 → 앞면: 순위 메달 · 1등은 왕관 · 「보물을 찾았습니다!」 · 몇 번째 · 게임 이름 · 상품(선물 그림) · 주최 · 찾은 시각 · 순위 · 주최자 안내) → 금박 위로 빛이 지나감 · 확인 · 뒤로가기는 언제든 확인 · 예전 그림 상자(Stage)·글 칸(Info)과 AR 쪽 뚜껑 열기 연출(축하 화면이 상자를 가져가면)은 지움 · 상세에서 코드로 찾아도 같은 연출(그 게임 모양으로 새 상자). v43: 10-24 대표님 「화면확대하거나 축소할 때 발생하는 확대축소 레이아웃도 조금 올려줘. 아이폰의 경우 노치랑 겹치지만 않으면 돼. 안드로이드도」 — 확대/축소 표시를 화면 위 400 고정(안전영역 무시)에서 안전영역(노치 · 상태바) 바로 아래 상태 알약 줄로 · 표시가 떠 있는 동안 상태 알약(주변 장소 · 로딩)과 하늘 칩은 곧장 숨음(장소 상세가 열려 있는 동안도) · 10-26 「1.0x 가 되었을 때 … 진동이 살짝 울리면서 snap」 — 핀치가 1.0x 에 붙고 붙는 순간 살짝 진동(ARObjectZoomController). v42:10-24 대표님 「위쪽으로 여백이 큰데 더블터치해서 발생하는 버튼이나 서브사진들 및 각 레이아웃들을 전체적으로 좀 올려야」 — 장소 상세 세로 배치를 165 위로(아이콘 줄 · 이름 · 로마자 · 만든 사람 줄 · 사진 · 정보 글 · 넘김 화살표 — 사진 쪽은 앵커만) · X 는 사진 아래끝에 걸치게 사진 앵커로 · 댓글 미리보기는 도크처럼 아래 안전영역만큼(아이폰에서 글이 도크 윗선에 붙던 것) · 미리보기 누름 칸이 X 아래쪽을 덮어 X 를 눌러도 댓글 창이 열리던 것 · 상세가 열려 있는 동안 노치 아래 상태 알약 숨김. v41:10-26 대표님 「필터에서 '반려견' 으로 있는 걸 '애견동반' 이라고 … 꺼져있는 거랑 '애견동반' 켜져있는 거 하나만 … 진한 노란색」 — 목록 필터 '반려견' → '애견동반' · 꺼짐(회색 · 전부) / 켜짐(꽉 찬 진한 노랑 · 애견동반 되는 곳만) 두 가지뿐(예전엔 옅은 노랑 '반려견' = 전부 · 꽉 찬 주황 '반려견만' + 반짝이는 점). v40: 10-26 대표님 「본인이 올린 오브젝트의 내용은 … 수정할 수 있도록 · 직접 삭제 · 각 사진 우측 위에 (메인사진포함) x버튼 · 위치가 바뀌었다면 바로 적용」 — 장소 수정 카드에 위치 줄(좌표·내 위치에서 거리·'여기로 옮기기')·사진 x(대표·추가 칸마다)·'삭제'(카드 오른쪽 위) · 저장 단추는 남의 장소면 '수정 요청'(내 장소면 코드가 '수정하기') · RemoveRequest 에 '···' 칸·묻는 글·단추 글 연결(내 장소면 '삭제'·'지우기'). v39: 10-25 대표님 「키패드 발생했을 때 입력칸 아래로 스와이프하면 키패드 사라지도록」 — 키보드가 떠 있을 때 입력칸을 아래로 쓸어내리면 키보드가 내려감(대화·댓글 입력줄 · 키보드 위 입력줄과 그 위 막 · 검색 등 칸 — 아이폰 기본 키보드엔 내리기 키가 없어 대화·댓글 창에선 창을 닫는 것 말고 길이 없었다) · 키보드 위 입력줄은 글을 원래 칸에 옮기고 닫음(보내기 칸이어도 안 보냄) · 손을 떼도 칸이 다시 열리지 않음 · 단추에서 시작한 손가락은 단추 몫 · 대화창 밀어 닫기는 그 손가락을 받지 않음 (R0926KeyboardSwipe). v38: 10-23 대표님 「'날씨' 를 옆으로 스와이프해서 … 근처 보물찾기 · 근처핫플 · 근처축제 … '언론사' 라이브」 — 하늘 날씨판을 좌우로 밀면 근처 보물찾기(내 범위 안 진행 중 · 곧 시작 — 새 게임 알림을 못 받은 사람도 눌러서 참가) · 근처 축제(50km 안 · 2주 안) · 근처 핫플(3km 안 요즘 좋아요 · 댓글 많은 곳 — 누르면 AR 의 그 박스를 짚음) · 근처 친구(위치 공유 맞팔 20km 안 — 누르면 프로필) · 언론사 라이브(YTN · 연합뉴스TV · KBS · MBC · SBS · JTBC 유튜브 생방송) · 내용 있는 장만 · 아래 점 · 처음엔 옆 장이 있다고 살짝 밀어 보임 · 날씨 조각을 날씨 장(Weather0926)으로 묶음 · 설정 '동네 이벤트 · 게임 (곧)' → '동네 소식 · 라이브'(실제로 켜고 끔) · 10-25 대표님 「조금만 움직여도 해당 내용이 사라질 수 있으니 … 고정」 — 판을 아래로 끌거나 📌 를 누르면 화면 아래쪽(주소줄 위) 유리 판으로 고정(휴대폰을 내려도 남음) · 위로 밀거나 📌 를 다시 누르면 하늘 자리로 · 한 번 뜬 판은 설정 각도보다 8° 더 내려야 사라짐 · 판을 만지는 동안은 기울어도 안 사라짐. v37: 10-23 대표님 「오프닝 뒷배경이 멀어지는 느낌」 — 시작화면 뒷배경(문양·성운·별)만 1.15배 확대에서 3.8초에 걸쳐 줌아웃(배경이 다 드러나는 2초 무렵 가장 빠르게 · 별은 가까울수록 조금 더 — 깊이) · 로고·반짝임·렌즈는 그대로 · 문양·성운의 숨쉬는 확대(sin — 3.7초 동안 커지기만 했다)는 뺌. v36: 10-23 대표님 요청분 — 목록 필터 칩 4칸×2줄(위 전체·반려견·공공데이터·축제 / 아래 지하철·터미널 / 기차·3D·사람) · 축제 칩(켜면 목록이 거리와 상관없이 근처 축제만 — 며칠부터·거리, 축제마다 그림, 누르면 검색) · 반려견만이면 가까운 유기견 보호소 3곳을 맨 위에 · 사람 칩이 꺼지면 '주변 사람' · 3D 칩은 켜지면 꽉 찬 보라 · 분류 목록 순서(전체·음식점·카페·샵·공원·스포츠·화장실·랜드마크)와 분류 그림 · 카페 모카색 · 스포츠 빨강. v35: 10-22 대표님 요청분 — 보물찾기 보물을 금빛 큐브 → 보물상자로(같은 금빛 · 짙은 청동 띠·귀 보호대·자물쇠 · 둥근 뚜껑 · 둘레에 흰 별빛이 피었다 사그라듦 · 제자리에서 천천히 돎) · 두 번 톡톡으로 찾으면 서버에 묻는 동안 달그락 → 이쪽을 보고 뚜껑이 열리며 빛기둥·별빛이 솟고 작아지며 사라짐 → 축하 화면이 사진 화면처럼 스르륵(열린 보물상자 · 금화 더미·보석 · 금화가 섞인 꽃가루) · v34: 10-21 대표님 요청분 — 지도의 친구 핀·친구 목록 줄을 누르면 그 사람 프로필(목록 시트는 닫았다가 프로필을 닫으면 보던 자리 그대로 다시 열림 — 프로필이 시트보다 아래 층이라 시트 뒤에 겹쳐 가려졌다) · 팔로우 중인 사람 프로필에 '팔로우 중 | 메세지 보내기' 한 줄 · 프로필 사진을 눌러도 대화창이 안 열리고 프로필만 닫히던 것 · v33: 10-21 대표님 「'3D모델 추가' 등을 눌러도 효과음이 안 난다」 — 추가 창 위 탭(장소·3D·보물)·분류 칩·목록 위 탭에도 다른 버튼과 같은 누름 소리 · v32: 10-21 대표님 요청분 — 3D 모델 파일 칸의 연두 체크 칸을 없애고 고른 파일 이름을 칸 아래 글자로 · 3D 모델 분류도 장소와 같은 여덟 가지(스포츠·랜드마크·기타 추가) · 사진·파일 칸의 '불러오는 중' 빈 칸이 누름을 막지 않게(답을 잃은 고르기 창은 칸을 다시 눌러 풂) · v31: 10-21 대표님 '기기 나라 언어로 다 뜨나' — 보물찾기 카드·수칙·상세·프로필 칸 글자를 일본어·중국어·스페인어로(예전엔 세 언어 모두 영어) · 프로필 '팔로우' 버튼 글자 연결(숨은 '팔로잉' 버튼 글자를 가리켜 보이는 버튼은 늘 한국어 '팔로우' — '맞팔로우'도 안 떴다) · v30: 10-21 대표님 요청분 — 장소 상세 '···' 시트를 사진 고르기 시트처럼 한 줄 큰 칸 넷('정보 수정 / 삭제 요청 / 신고 / 취소' — 예전 세 줄 목록 + 카드 밑 따로 뜬 취소 알약, 글자 44 · 그림 60) · 칸 글자가 외국어로 길면 칸 안에 들게 줄임(사진 시트도) · v29: 10-20 대표님 요청분 — 대표 사진 자르기 화면을 새로(가운데 정사각형 틀은 그대로 · 사진을 끌어 옮기고 두 손가락으로 키우고 줄임 · 두 번 톡 = 크게/다시 맞춤 · 만지는 동안 3x3 줄 · 아래 한 줄 큰 세 칸 '취소 / 회전 / 완료' · 하단 메뉴와 같은 물방울 소리 · 뒤로가기 = 취소 — 예전 ImageCropper 는 작은 네모를 모서리 손잡이로 줄이는 방식에 위쪽 작은 아이콘·소리 없음) · 취소해도 '자르기 실패'가 뜨지 않음 · 처리 중 카드(도는 분홍 원호 + '자르는 중…' / '업로드 중… N%' / '보내는 중… N%' + 막대 — 예전엔 2초짜리 경고 글에 퍼센트만, 스피너는 추가 창 뒤에 가려 안 보였다) · 올리는 동안 다른 곳과 뒤로가기를 막음 · v28: 10-17~10-20 대표님 요청분 — 시작화면 렌즈(유리 빛 음영 · 위쪽 테두리를 미끄러지는 반짝임 · 렌즈 안 1.06배 돋보기 · 퍼질 때 그늘이 걷히며 테두리가 번져 사라짐 · 원 밖 그늘 0.9 · 얇은 흰 테두리에 안팎 페더 · 예전 흰 링 그림 삭제 · 별·렌즈에 CanvasRenderer) · 첫 안내 크게·위로(글 74 · 아랫줄 50 · 다음 440x150 · 버튼 위 화살표 + 글에서 화살표로 점선 길 · 칸 둘레 검은 네모 없앰 · 가로 화면에선 '아래' 빼고) · 안드로이드 효과음 버퍼를 가장 짧게(누른 뒤 한 박자 늦던 것) · 장소 추가 '생성 중' — 생길 자리가 화면 밖이면 큐브 화살표 2배 크기 흰 화살표가 반짝이며 가리킴 · 생성 중에 취소하면 큐브가 남지 않음 · 사진을 불러오는 동안 그 칸에 빈 칸 + 도는 원호(대표·추가 사진 — 장소·3D모델·장소 수정) · 추가 사진 열 장도 자리 안 · 정사각 썸네일 · '촬영 / 갤러리 / 취소' 한 줄 큰 세 칸 · 사진 시트 뒤 어둡게 · '계속 촬영?' 시트 키움 · 갤러리 취소에 '선택 실패'·대표사진 숨김 없앰 · 카메라 취소에 '권한 필요' 없앰 · 남은 칸보다 많이 고르면 남은 만큼 · 설정 ▸ 장소 '멀리 있는 오브젝트 흐리게' 끄기/약하게/보통(기본)/강하게 · v27: 10-19 대표님 요청분(브이월드 운영키) — 도크 위 한 줄 주소를 우팡 서버가 브이월드에 물어 '대흥면 지곡리'처럼(해외·서버 안 될 땐 예전 길) · 같은 자리(25m 안)는 다시 묻지 않음 · 지도 검색이 좌표뿐 아니라 주소·장소·동네 이름도 찾음(한 곳이면 바로 그 자리로, 여럿이면 검색 줄 아래 목록 다섯 줄 · 지금 보는 곳 둘레부터) · 꾹 눌러 꽂은 핀 말풍선 위 줄에 그 자리 주소 · 검색한 장소는 핀 위에 이름 · '강남대로 123길 45' 같은 주소를 좌표로 잘못 읽던 것 · v26: 10-13·10-15·10-17·10-18 대표님 요청분 — 목록 필터: '전체' 칩을 누르면 분류 8개가 칩 아래로 펼쳐져 고름(누를 때마다 다음 분류로 넘어가던 것) · 칩 두 줄 위 4칸(전체·반려견·공공데이터·3D) 아래 3칸(지하철·터미널 / 기차·사람) · 기차역 칩을 터미널과 합침(기차역 칩이 숨은 '주류' 필터로도 이어져 끄면 술 파는 가게까지 숨던 것) · '공공'→'공공데이터'(기관이 올린 장소·관광공사만 — 사람이 '공원'으로 올린 곳은 우팡 데이터) · 켜진 흰 칩(3D·전체·팔로잉)은 꽉 찬 흰색 · 반려견은 두 가지('애견동반 안 됨' 없앰) · '목록|지도|설정' 칸을 필터 칩 높이로 · 'AI에게 묻기' 숨김 · 목록에 참가 중·내가 연 보물찾기 줄(금빛 상자·공개 설정대로 거리 · 누르면 그 게임 상세) · 지도에도 그 보물찾기(정확히 공개는 그 자리 · 대략 공개는 원 · 누르면 그 게임 상세 · 게임이 있으면 위치 공유를 꺼 두어도 지도를 가리지 않음) · 지도 한 단계 더 확대(서버 타일 단계 + 1 · 지도 좌표 double) · 지도는 보던 자리를 기억(다른 창에 다녀오거나 앱을 다시 켜도) · 지도를 꾹 누르면 핀 + 위도·경도 복사 · 돋보기로 좌표 검색(구글 지도 주소·도분초도 · 붙여넣기) → 그 자리에 핀 + 동네 보이게 확대 · 설정의 '라이브 방송'·'광고 · 제휴 소식' 줄 삭제(보낼 내용 없이 기기 저장값만 있던 '곧' 스위치) · 근처 사람은 사람 모양(핑크) · 사람 칩 '팔로잉'이 실제로 걸리게(필터 둘이 따로 돌던 것 · 켤 때 늘 '제외' · 팔로잉 목록을 로그인 전에 받아 비던 것) · 목록·카드 옆으로 밀다 아래로 끌면 맨 위에서도 내려가던 것(R0926ScrollLock) · 메시지 검색에서 바로 팔로우·프로필 · 메시지 창 제목 아래 작은 '(친구찾기)'(도크 칸엔 없음 — 창이 열렸을 때만) · 앱 최소 글자 38 · 토스트 · 입력칸 분홍 테두리·커서·입력줄 위로 스크롤 · 10-13 대표님 겹침 — 보물찾기 상세로 갈 때(알림·말풍선·프로필 게임 줄) 프로필·팔로우 목록·대화방·메시지 목록은 열어 준 곳으로 되돌아가지 않고 바로 닫힘(팔로잉 목록 → 프로필 → 게임 줄 · 대화방 → 말풍선) · 메시지 목록 X 가 자기를 꺼 다른 길로 다시 켜지면 닫히지 않던 것 · 대화창의 '[보물찾기 · 제목]' 말풍선은 금색 머리 줄 · 한 번 누르면 그 게임 상세(두 번은 좋아요) · v25: 10-13·10-17 대표님 요청분 — 보물찾기 '보물 열기' 버튼을 없애고 AR 금빛 상자를 두 번 톡톡 눌러 열기 · 찾으면 축하 화면(선물 상자·빛줄기·꽃가루 + 상품 안내 + 주최자에게 메세지) · 보물은 제자리(위아래로 안 떠다님) · 화면 밖 금색 박스에 별빛 · 주최자도 자기 보물이 보임 · '1시간 연장하기 / 1일 연장하기' 한 줄·누르자마자 반영 · 한마디·코드 칸도 입력줄 보내기 화살표 · 보물찾기 카드 글자·버튼 키움(돌아가기 알약·주최 수칙 보기) · 추가 창 '장소/3D모델/보물찾기' 탭 글자를 '분류'만큼 · v24: 10-12 추가 — 보물찾기 참가 인원 상한 칩 · 날씨판 지역을 기온 바로 위 유리 알약(핀 + 이름)으로 · 10-06 대표님 요청분 — 대화창을 새 모양으로(내 말 분홍·상대 카드색 둥근 말풍선 · 날짜 알약 · ‹ 와 흰 제목, 우팡 공식 방은 체크 배지 · 좋아요 하트가 안 뜨던 것) · 설정 '화면' 30/60fps(기본 30) · 버전은 설정 맨 아래 작게 · 저작권 자리는 대표님이 씬에서 잡은 그대로 · 목록 글자 키움(이름 48·거리 41) · AR 복구 중에도 도크 누름(불러오는 중 막이 칩·도크 아래로) · v23: 10-06 대표님 대조분 — 첫 안내 다음·시작하기·건너뛰기 소리를 하단 메뉴(물방울)와 같게 · 날씨판 지역은 서버(카카오 행정구역) 우선 · 목록 맨 아래 버전만(접고 펴기 없앰)·저작권 그림 글만 잘라 키움 · 설정 '앱 정보'(문의·개인정보·출처·버전) · 추가 창 세 번째 카드 '보물찾기'(장소|3D모델|보물찾기 탭 · 숨기기·참가·상세) · v22: v21 검토분 — 장소 수정 X 덮개가 다시 적용 때 꺼지던 것 · X 가 자기를 꺼 두 번째부터 사라지던 것 · 친구 이름 글자 잘림 · 'Anuncio' 배지 · v21: 목록 시트 제목·'목록|지도|설정' 글자 키움 · 지도·설정 탭 글자·줄 키움 · 지도 깊은 확대(서버 타일 · 없으면 200배) · 장소 수정 카드도 밀어 닫기 · v20: v19 검토분 — 밀어 닫을 때 시작한 버튼(등록하기·팔로우·로그아웃)이 눌리던 것 · 추가 시트 위쪽을 덮던 투명 누름 영역(XButton_Panel) · 프로필 내릴 때 도크 X 가 카드 위로 비치던 것 · v19: 창 닫기 통일 — 추가·프로필도 아래로 밀어 닫기(추가는 장소/3D모델 넘기기와 같은 방향 잠금) · 추가 카드 손잡이 · 프로필 열리면 도크 칸 X '닫기' · '되돌리기' 알림은 창이 열리면 거둠·가로 자리 · 업로드 제한시간 60초(3D 모델·정보 수정) · v18: 첫 안내 목록→추가→메세지 순서·다음/시작하기 터치음 · 도크 '메세지' · 숨김 되돌리기 알림·설정 '모두 다시 보이기' · 날씨판 위 지역 이름 · 업데이트 카드 '반영 중' 상태 제거 · v17: 업데이트 안내를 프로필 카드 모양으로 · v16: 키보드 위 입력줄 연결 바로잡음(장소 칸 글 덮어쓰던 것·장소 수정 쪽 빈 연결)·폭 · 장소 추가 칩·스위치 키움 · v15: 10-04 아이폰 확인 — 채팅방이 안 열리던 것(키보드 처리기) · 대화 목록 두 벌 · 도크 메시지 칸 · 손잡이 · 프로필 카드 · 입력줄 · 장소 추가 크게·좌표 · 날씨 크기 · v14: 0930 시안 — 도크 여백·테두리 추가·한 줄 위치 · 창이 도크 윗선에서 오르내림 · 프로필 페이드 · 칩·목록 촘촘히 · 탭 옆으로 밀기 · 상태 알약 · 안내 스포트라이트 · 날씨 예보 · 댓글 입력 · X 두 번 눌러 삭제 · v13: 시작화면 영어(우주) 하나로 · 도크 버튼 키움 · 닫을 때도 아래로 미끄러짐 · v12: 시작화면 색동·금테 제거 · 렌즈 밖 옅은 문양 · 흰 렌즈 · 워드마크 간격 · 끌어 닫기 · 뒤로가기 정리 · v11: 시작화면을 맨 위 하나로 (예전 시작 이미지 제거 · Panel_Top 정리) · v10: 시작화면 SEE THE UNSEEN (v9: 친구 지도 · v8: 인디케이터 X)   // v6: 거리별 흐림 · 가로 화면 (v5: 장소 추가 개편 · 상태 화면 · 안내 · 끊김 배너 · 로마자)

        private const string SpriteDir = "Assets/Redesign0926/Sprites/";

        // ── 색 ─────────────────────────────────────────────
        private static readonly Color Glass = new Color(0.055f, 0.067f, 0.078f, 0.58f);
        private static readonly Color Sheet = new Color(0.078f, 0.090f, 0.102f, 0.97f);
        private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
        private static readonly Color Soft = new Color(0.867f, 0.886f, 0.902f, 1f);
        private static readonly Color Muted = new Color(0.549f, 0.584f, 0.616f, 1f);
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f, 1f);
        private static readonly Color Dark = new Color(0.082f, 0.094f, 0.106f, 1f);

        // ── 크기 (캔버스 단위) ─────────────────────────────
        private const float Side = 60f;          // 좌우 여백 16pt
        private const float TopGap = 36f;        // 상태바 아래 여백
        private const float TopButton = 148f;    // 로고·프로필 원 40pt
        private const float DockHeight = 224f;   // 60pt — 2026-09-30 위아래 여백을 줄여 달라 하셔서 (아이콘 크기는 그대로)
        private const float DockBottom = 30f;    // 홈 인디케이터 위 8pt
        private const float PlusSize = 196f;     // 52pt
        private const float DockIcon = 106f;     // 29pt (예전 25pt)
        private const float DockLabel = 44f;     // 12pt (예전 10pt)
        private const float DockIconY = 26f, DockLabelY = -58f, DockLabelBox = 60f, AddGlyph = 76f;

        private static Font font;

        public static bool ApplyIfOutdated(Scene scene, List<string> log)
        {
            var canvas = Redesign0926Capture.FindMainCanvas(scene);
            if (canvas == null) return false;
            var marker = canvas.GetComponentInChildren<R0926Marker>(true);
            if (marker != null && marker.appliedVersion >= Version) return false;
            Apply(scene, log);
            return true;
        }

        public static void Apply(Scene scene, List<string> log)
        {
            EnsureSpriteImports(log);
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/AppleSDGothicNeoM.ttf");

            var canvas = Redesign0926Capture.FindMainCanvas(scene);
            if (canvas == null) { log.Add("Canvas 없음"); return; }
            Transform root = canvas.transform;
            Unwrap0930(root);                   // 지난번 틀(Clip0926·ListPage0926)을 벗기고 — 아래 단계들은 패널 기준으로 다시 잡는다

            ApplyTop(root, log);
            ApplyDock(root, log);
            ApplyLocationChip(root, log);
            ApplyListSheet(root, log);
            ApplyDialogs(root, log);
            ApplyPlaceMenu(root, log);
            ApplyMessages(root, log);
            ApplyProfile(root, log);
            ApplyUpload(root, log);
            ApplyUploadFull(root, log);
            ApplyStatusScreens(root, log);
            ApplyGuideNetRoman(root, log);
            ApplySkyAndAI(root, log);
            ApplyFriendsMap(root, log);
            ApplySheetClosing(root, log);       // 끌어 닫기 · 뒤로가기 — 시트·버튼이 다 자리잡은 뒤
            ApplySplash(root, log);             // 시작화면은 맨 위 — 마지막에
            ApplyDetailLift(root, log);         // 장소 상세 세로 배치를 위로 — 0930 의 아래 안전영역 덜 밀기가 미리보기에도 걸리게 그보다 먼저
            Apply0930(root, log);              // 0930 시안 — 틀을 다시 씌우고 새 모양 (시작화면 별 포함)
            ApplyTopSlot(root, log);            // 노치 아래 한 줄 — 확대/축소 표시 · 상태 알약 · 하늘 칩 하나씩 (0930 의 상태 알약이 생긴 뒤에)
            ApplyFadeAndLandscape(root, log);   // 가로 배치는 세로 값이 다 정해진 뒤에 (0930 틀 포함 — 맨 마지막)
            ApplyListKinds(root, log);          // 목록의 사람·보물찾기 줄 그림 · 사람 필터 하나로
            ApplyListFilters(root, log);        // 목록 필터 — '전체' 고르기 목록 · 'AI에게 묻기' 숨김 (0930 틀 안에)
            ApplyMapCoord(root, log);           // 지도 꾹 눌러 핀 · 좌표 검색 (입력칸 다듬기보다 먼저)
            ApplyCrop(root, log);               // 대표 사진 자르기 화면 · 처리 중 카드 (도크 소리를 쓰므로 도크 뒤)
            ApplyReadable(root, log);           // 토스트 · 앱 전체 최소 글자 · 입력칸 커서 — 다른 단계가 글자 크기를 다 정한 뒤에
            ApplySkyPages(root, log);           // 하늘 날씨판 옆 장 — 날씨판이 다 자리잡은 뒤에
            CloseStartPanels(root, log);

            var marker = root.GetComponentInChildren<R0926Marker>(true);
            if (marker == null)
            {
                var mgo = new GameObject("Redesign0926Marker", typeof(RectTransform), typeof(R0926Marker));
                mgo.transform.SetParent(root, false);
                marker = mgo.GetComponent<R0926Marker>();
            }
            marker.appliedVersion = Version;
            EditorUtility.SetDirty(marker);
            log.Add("apply v" + Version + " 완료");
        }

        // 앱을 켰을 때 닫혀 있어야 하는 창 — 씬에서 고치느라 켜 둔 채 저장해도 적용 때 다시 끈다 (BuildValidator 도 같은 목록으로 막는다)
        // 2026-10-11: 저작권 자리를 잡느라 켠 ListPanel 이 켜진 채 저장돼, 앱을 켜자마자 목록이 떠 있고 추가 창까지 덮었다
        public static readonly string[] StartClosedPanels = { "ListPanel", "UploadPage", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "Fixpage", "FollowPanel", "LoginPromptPanel", "AskAISheet0926", "FullScreenPanel", "PhotoCrop0926", "Busy0926" };

        private static void CloseStartPanels(Transform root, List<string> log)
        {
            foreach (var n in StartClosedPanels)
            {
                var p = Find(root, n);
                if (p == null || !p.activeSelf) continue;
                p.SetActive(false);
                log.Add("켜진 채 저장돼 있던 창을 끔: " + n);
            }
        }

        // ============================================================
        // 상단 — 검은 띠 대신 옅은 그늘 + 로고 엠블럼(누르면 색 테마) + 프로필(오른쪽)
        // ============================================================
        private static void ApplyTop(Transform root, List<string> log)
        {
            var top = Find(root, "Panel_Top");
            if (top == null) { log.Add("Panel_Top 없음"); return; }

            // 위쪽은 비운다 — 상태바 글자가 보이게 옅은 그늘만. 로고는 시작 화면에서만 보여 준다
            SetRect(RT(top), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 300));
            var bg = top.GetComponent<Image>();
            bg.sprite = Spr("r0926_scrim");
            bg.type = Image.Type.Simple;
            bg.color = new Color(0, 0, 0, 0.45f);
            bg.raycastTarget = false;

            // 로고가 없으니 로고·로고 받침은 지운다. 색 테마도 상단 띠만 칠하던 것이라 끈다 —
            // 남기면 저장된 테마색(분홍 등)이 상단을 통째로 덮는다. Panel_Top 은 상태바 글자용 그늘로만 남긴다
            foreach (var n in new[] { "TITLE_Woopang", "EmblemGlass0926" })
            {
                var old = Find(top.transform, n);
                if (old != null) { Object.DestroyImmediate(old); log.Add("top: " + n + " 삭제"); }
            }
            var changer = top.GetComponent<TopPanelColorChanger>();
            if (changer != null)
            {
                var cso = new SerializedObject(changer);
                cso.FindProperty("sceneColorOnly").boolValue = true;
                cso.FindProperty("logoImage").objectReferenceValue = null;
                cso.FindProperty("currentColorIndex").intValue = 0;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("top ok");
        }

        // ============================================================
        // 하단 도크 — 목록 · 올리기 · 메시지를 한 줄로 (버튼 오브젝트는 그대로 옮긴다)
        // ============================================================
        // 도크 칸 위치 (도크 너비 기준 0~1)
        private const float SlotList = 0.125f, SlotAdd = 0.375f, SlotMsg = 0.625f, SlotProfile = 0.875f;
        private const float AddTile = 106f;
        private const float MiniAvatar = 92f;   // 도크 프로필 사진 — 테두리까지 다른 아이콘과 비슷한 크기

        private static void ApplyDock(Transform root, List<string> log)
        {
            var listBtn = Find(root, "List_Button");
            var plusBtn = Find(root, "PlusButton");
            var msgBtn = Find(root, "Message_Button");
            var mini = Find(root, "MiniProfile");
            if (listBtn == null || plusBtn == null || msgBtn == null) { log.Add("도크 버튼 없음"); return; }

            var dock = Find(root, "Dock0926");
            if (dock == null)
            {
                dock = NewUI("Dock0926", root);
                dock.transform.SetSiblingIndex(listBtn.transform.GetSiblingIndex());
            }
            Img(dock, Spr("r0926_pill"), Glass, Image.Type.Sliced, 64f / (DockHeight / 2)).raycastTarget = true;
            SetRect(RT(dock), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, DockBottom), new Vector2(-Side * 2, DockHeight));
            Ensure<R0926SafeInset>(dock).SetEdge(R0926SafeInset.Edge.Bottom);

            DockButton(listBtn, dock.transform, SlotList, "r0926_i_list", "목록", "List", "リスト", "列表", "Lista");
            DockButton(msgBtn, dock.transform, SlotMsg, "r0926_i_mail", "메세지", "Messages", "メッセージ", "消息", "Mensajes");   // 한국어는 '메세지' (2026-10 요청)
            // '(친구찾기)' 는 도크 칸이 아니라 메시지 창 제목 아래에만 (10-19 대표님 — MessageTitleSub) · 예전에 칸에 붙인 것 지움
            var oldSub = msgBtn.transform.Find("SubLabel0926");
            if (oldSub != null) Object.DestroyImmediate(oldSub.gameObject);

            // 안 읽음 표시 — 이름·부모 그대로 (MessagePanelManager 가 이름으로 찾는다)
            var unread = Find(msgBtn.transform, "UnreadMessageButtonImage");
            if (unread != null)
            {
                var u = unread.GetComponent<Image>();
                u.sprite = Spr("r0926_pill");   // 숫자가 두 자리가 되면 옆으로 늘어나는 알약 모양
                u.type = Image.Type.Sliced;
                u.pixelsPerUnitMultiplier = 64f / 29f;
                u.color = Pink;
                u.raycastTarget = false;
                SetRect(RT(unread), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(58, DockIconY + 42), new Vector2(60, 60));   // 아이콘 오른쪽 위 모서리
                // 검은 테두리 없이 (예전 버튼에서 따라온 Outline·Shadow)
                foreach (var fx in unread.GetComponentsInChildren<Shadow>(true)) Object.DestroyImmediate(fx);
                unread.transform.SetAsLastSibling();
                var cnt = FindOrCreate(unread.transform, "Count");   // MessagePanelManager 가 개수를 적는다
                var crt2 = RT(cnt); crt2.anchorMin = Vector2.zero; crt2.anchorMax = Vector2.one; crt2.offsetMin = Vector2.zero; crt2.offsetMax = Vector2.zero;
                var ctx = Txt(cnt, "3", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                ctx.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            // 추가 — 다른 칸과 같은 크기, 아이콘만 흰 사각 타일로 강조 + 아래 '추가'
            AddButtonStyle(plusBtn, dock.transform, SlotAdd, "r0926_i_plus", new[] { "추가", "Add", "追加", "添加", "Añadir" });

            // 프로필 — 오른쪽 끝 칸. 로그인 전엔 '로그인'
            if (mini != null)
            {
                mini.transform.SetParent(dock.transform, false);
                NoInset(mini);
                var mrt = RT(mini);
                mrt.localScale = Vector3.one;
                SetRect(mrt, new Vector2(SlotProfile, 0.5f), new Vector2(SlotProfile, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));
                var mImg = mini.GetComponent<Image>();
                if (mImg != null) { mImg.color = new Color(0, 0, 0, 0); mImg.raycastTarget = true; }
                var mask = Find(mini.transform, "AvatarMask");
                if (mask != null) SetRect(RT(mask), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(MiniAvatar, MiniAvatar));
                // 테두리는 사진 위에 — 안쪽 가장자리가 사진 가장자리를 살짝 덮어 계단을 가린다 (r0926_ring_mini: 안 0.68 · 진함 ~0.86 · 바깥으로 옅게)
                var ring = Find(mini.transform, "AvatarOutline");
                if (ring != null)
                {
                    float rs = (MiniAvatar / 2f - 3f) / 0.68f * 2f;
                    SetRect(RT(ring), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(rs, rs));
                    var ri = ring.GetComponent<Image>();
                    ri.sprite = Spr("r0926_ring_mini"); ri.type = Image.Type.Simple; ri.raycastTarget = false; ri.preserveAspect = true;
                    EditorUtility.SetDirty(ri);
                    if (mask != null) ring.transform.SetSiblingIndex(mask.transform.GetSiblingIndex() + 1);
                    // 전체공개면 은은하게 숨쉬고 4초마다 물결 한 번
                    var rip = FindOrCreate(mini.transform, "Ripple0926");
                    rip.transform.SetSiblingIndex(ring.transform.GetSiblingIndex());
                    SetRect(RT(rip), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(rs * 0.86f, rs * 0.86f));
                    var ripImg = Img(rip, Spr("r0926_ring_big"), new Color(Pink.r, Pink.g, Pink.b, 0f), Image.Type.Simple);
                    ripImg.raycastTarget = false;
                    ripImg.enabled = false;
                    var pulse = Ensure<R0926AvatarPulse>(mini);
                    var pso = new SerializedObject(pulse);
                    pso.FindProperty("outline").objectReferenceValue = ri;
                    pso.FindProperty("ripple").objectReferenceValue = ripImg;
                    pso.FindProperty("breathScale").floatValue = 0.035f;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }
                var uname = Find(mini.transform, "Username");
                if (uname != null) uname.GetComponent<Text>().enabled = false;   // ProfileManager 는 계속 쓴다 — 보이지만 않게
                var lb = FindOrCreate(mini.transform, "Label0926");
                Txt(lb, "프로필", DockLabel, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
                SetRect(RT(lb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockLabelY), new Vector2(280, DockLabelBox));
                var old = lb.GetComponent<R0926LocalizedText>();
                if (old != null) Object.DestroyImmediate(old);
                Ensure<R0926ProfileLabel>(lb);
            }
            log.Add("dock ok");
        }

        private static void AddButtonStyle(GameObject btn, Transform parent, float x, string glyph, string[] words)
        {
            btn.transform.SetParent(parent, false);
            var rt = RT(btn);
            rt.localScale = Vector3.one;
            SetRect(rt, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));
            var img = btn.GetComponent<Image>();
            img.sprite = null; img.color = new Color(1, 1, 1, 0); img.raycastTarget = true; img.preserveAspect = false;
            foreach (var t in btn.GetComponentsInChildren<Text>(true))
                if (t.name != "Label0926") t.enabled = false;

            // 0930: 채운 흰 타일 대신 흰 테두리 네모 + 흰 + (닫기는 다른 칸처럼 흰 X) — 아이콘 크기는 다른 칸과 같게
            var tile = FindOrCreate(btn.transform, "Icon0926");
            tile.transform.SetSiblingIndex(0);
            var tImg = Img(tile, Spr(glyph == "r0926_i_plus" ? "r0926_i_addbox" : glyph), Ink, Image.Type.Simple);
            tImg.raycastTarget = false;
            SetRect(RT(tile), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(DockIcon, DockIcon));
            var g = FindOrCreate(tile.transform, "Glyph0926");   // 예전 타일 속 글리프 — 남겨 두되 보이지 않게
            Img(g, Spr(glyph), Dark, Image.Type.Simple).raycastTarget = false;
            g.GetComponent<Image>().enabled = false;

            var lb = FindOrCreate(btn.transform, "Label0926");
            Txt(lb, words[0], DockLabel, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(lb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockLabelY), new Vector2(280, DockLabelBox));
            Loc(lb, words[0], words[1], words[2], words[3], words[4]);

            var b = btn.GetComponent<Button>();
            if (b != null) { b.targetGraphic = tImg; EditorUtility.SetDirty(b); }
        }

        private static void DockButton(GameObject btn, Transform dock, float x, string icon,
            string ko, string en, string ja, string zh, string es)
        {
            btn.transform.SetParent(dock, false);
            var rt = RT(btn);
            rt.localScale = Vector3.one;
            SetRect(rt, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));

            // 버튼 자체는 투명한 누름 영역 — 모양은 아이콘·글자가 맡는다
            var img = btn.GetComponent<Image>();
            img.sprite = null;
            img.color = new Color(1, 1, 1, 0);
            img.raycastTarget = true;

            var ic = FindOrCreate(btn.transform, "Icon0926");
            ic.transform.SetSiblingIndex(0);
            var icImg = Img(ic, Spr(icon), Ink, Image.Type.Simple);
            icImg.raycastTarget = false;
            SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, DockIconY), new Vector2(DockIcon, DockIcon));

            var lb = FindOrCreate(btn.transform, "Label0926");
            lb.transform.SetSiblingIndex(1);
            Txt(lb, ko, DockLabel, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(lb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, DockLabelY), new Vector2(280, DockLabelBox));
            Loc(lb, ko, en, ja, zh, es);

            var b = btn.GetComponent<Button>();
            if (b != null) { b.targetGraphic = icImg; EditorUtility.SetDirty(b); }
        }

        // ============================================================
        // 위치 상태 — 도크 위 작은 유리 칩
        // ============================================================
        // AR 화면을 덮는 창 — 열려 있으면 위치 칩이 숨고 보물찾기 알림도 걷힌다
        private static readonly string[] ArCovers = { "FullScreenPanel", "ListPanel", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "FollowPanel", "UploadPage", "Fixpage", "FirstTimeGuidePanel" };

        private static void ApplyLocationChip(Transform root, List<string> log)
        {
            var panel = Find(root, "LocationManagerPanel");
            if (panel == null) { log.Add("LocationManagerPanel 없음"); return; }

            Img(panel, Spr("r0926_pill"), Glass, Image.Type.Sliced, 64f / 50f).raycastTarget = false;
            SetRect(RT(panel), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, DockBottom + DockHeight + 24), new Vector2(600, 84));
            Ensure<R0926SafeInset>(panel).SetEdge(R0926SafeInset.Edge.Bottom);

            var hlg = Ensure<HorizontalLayoutGroup>(panel);
            hlg.padding = new RectOffset(28, 38, 14, 14);
            hlg.spacing = 14;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var csf = Ensure<ContentSizeFitter>(panel);
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var status = Find(panel.transform, "StatusImage");
            if (status != null)
            {
                var le = Ensure<LayoutElement>(status);
                le.preferredWidth = 38; le.preferredHeight = 38;
                status.GetComponent<Image>().preserveAspect = true;
            }
            var text = Find(panel.transform, "LocationText");
            if (text != null)
            {
                var t = text.GetComponent<Text>();
                t.fontSize = 36;
                t.lineSpacing = 1.05f;
                t.color = Soft;
                t.supportRichText = true;   // 좌표는 옅게
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                if (font != null) t.font = font;
                EditorUtility.SetDirty(t);
            }
            // 상세·목록·메시지 등이 열려 있으면 숨긴다 (특히 상세 화면은 칩보다 아래에 그려져 겹친다)
            Ensure<CanvasGroup>(panel);
            // 주소·좌표 한 줄 (LocationManager)
            var lm = FindInScene<LocationManager>(panel);
            if (lm != null)
            {
                var lso = new SerializedObject(lm);
                var sl = lso.FindProperty("singleLine");
                if (sl != null) { sl.boolValue = true; lso.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var hide = Ensure<R0926HideWhile>(panel);
            var hso = new SerializedObject(hide);
            var arr = hso.FindProperty("panels");
            arr.arraySize = ArCovers.Length;
            for (int i = 0; i < ArCovers.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = Find(root, ArCovers[i]);
            hso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("location chip ok");
        }

        // ============================================================
        // 목록·필터 — 아래에서 올라오는 시트, 위아래 여백 최소
        // ============================================================
        private const float SheetPad = 48f;
        private const float HeaderY = -40f;
        private const float HeaderH = 124f;       // 제목줄 — 10-18 대표님 '목록 / 지도 / 설정' 을 필터 칩만큼(칸 112 = 칩 높이) · 10-05 84 · 예전 72
        private const float SliderBlock = HeaderH + 56f + 12f;   // 제목줄 + 슬라이더(56) + 틈
        private const float ChipsY = HeaderY - SliderBlock - 20f;   // -200
        private const float ChipH = 112f;   // 0930: 글자를 키운 칩 (예전 76)
        private const float ChipGap = 18f;
        private const float SummaryY = ChipsY - (ChipH * 2 + ChipGap) - 22f;   // -392
        private const float ListTop = SummaryY - 50f;                          // -442
        private const float ListBottom = 24f;   // 목록은 시트 아래 끝까지 (2026-10-23 대표님 — 저작권 그림은 설정 › 앱 정보로 옮겼다)
        private const float RowH = 96f;    // 1006: 글자가 작다(대표님) — 이름 38→48 · 거리 33→41 (0930 엔 66 으로 촘촘하게, 예전 84)
        private const float CloseSize = 72f;

        private static void ApplyListSheet(Transform root, List<string> log)
        {
            var panel = Find(root, "ListPanel");
            if (panel == null) { log.Add("ListPanel 없음"); return; }
            var pImg = panel.GetComponent<Image>();
            if (pImg != null) { pImg.color = new Color(0, 0, 0, 0.35f); }

            var sheet = Find(panel.transform, "Sheet0926");
            if (sheet == null) { sheet = NewUI("Sheet0926", panel.transform); }
            sheet.transform.SetSiblingIndex(0);
            CardAboveDock(sheet, 0.92f);   // 0930: 장소 추가 창과 같은 높이
            Slide(sheet, 240f);

            var grab = FindOrCreate(sheet.transform, "Grab0926");
            Img(grab, Spr("r0926_pill"), new Color(1, 1, 1, 0.22f), Image.Type.Sliced, 64f / 6f).raycastTarget = false;
            SetRect(RT(grab), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(110, 12));

            // 제목줄: "근처 장소 24" (기존 "ACTIVE LIST" 텍스트를 옮겨 쓴다)
            var header = FindOrCreate(sheet.transform, "Header0926");
            SetRect(RT(header), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(SheetPad, HeaderY), new Vector2(700, HeaderH));
            var hl = Ensure<HorizontalLayoutGroup>(header);
            hl.spacing = 14; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            var back = Find(panel.transform, "Scroll View Back");
            var title = Find(panel.transform, "Title");
            if (title != null && title.transform.parent != header.transform && (back == null || title.transform.IsChildOf(back.transform)))
                title.transform.SetParent(header.transform, false);
            if (title != null)
            {
                title.transform.SetSiblingIndex(0);
                var t = Txt(title, "근처 장소", 60, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                Loc(title, "근처 장소", "Nearby", "近くの場所", "附近地点", "Cerca");
            }
            var count = FindOrCreate(header.transform, "Count0926");
            count.transform.SetSiblingIndex(1);
            var ct = Txt(count, "", 60, Pink, TextAnchor.MiddleLeft, FontStyle.Bold);
            ct.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 거리 슬라이더
            var sliderUI = Find(panel.transform, "DistanceSliderUI");
            if (sliderUI != null)
            {
                sliderUI.transform.SetParent(sheet.transform, false);
                var sImg = sliderUI.GetComponent<Image>();
                if (sImg != null) sImg.color = new Color(0, 0, 0, 0);
                SetRect(RT(sliderUI), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, HeaderY), new Vector2(0, SliderBlock));
                RT(sliderUI).offsetMin = new Vector2(SheetPad, RT(sliderUI).offsetMin.y);
                RT(sliderUI).offsetMax = new Vector2(-SheetPad, RT(sliderUI).offsetMax.y);

                var slider = Find(sliderUI.transform, "DistanceSlider");
                if (slider != null)
                {
                    SetRect(RT(slider), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(0, 56));
                    var sbg = Find(slider.transform, "Background");
                    if (sbg != null)
                    {
                        Img(sbg, Spr("r0926_pill"), new Color(1, 1, 1, 0.14f), Image.Type.Sliced, 64f / 5f);
                        SetRect(RT(sbg), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 10));
                    }
                    var fillArea = Find(slider.transform, "Fill Area");
                    if (fillArea != null)
                    {
                        SetRect(RT(fillArea), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 10));
                        var fill = Find(fillArea.transform, "Fill");
                        if (fill != null) Img(fill, Spr("r0926_pill"), Ink, Image.Type.Sliced, 64f / 5f);
                    }
                    var handle = Find(slider.transform, "Handle");
                    if (handle != null)
                    {
                        Img(handle, Spr("r0926_circle"), Color.white, Image.Type.Simple);
                        RT(handle).sizeDelta = new Vector2(56, 0);
                    }
                    var handleArea = Find(slider.transform, "Handle Slide Area");
                    if (handleArea != null) SetRect(RT(handleArea), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-56, 0));
                }
                var val = Find(sliderUI.transform, "DistanceValueText");
                if (val != null)
                {
                    var vt = Txt(val, null, 36, Soft, TextAnchor.MiddleRight, FontStyle.Normal);
                    SetRect(RT(val), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(300, 72));
                }
            }

            // 필터 칩 두 줄 — 위 4칸 · 아래 4칸 (Redesign0926ApplyFilters)
            var filters = Find(panel.transform, "FilterButtonPanel");
            if (filters != null)
            {
                filters.transform.SetParent(sheet.transform, false);
                filters.transform.localScale = Vector3.one;   // 원래 2배 확대가 걸려 있었다
                var fImg = filters.GetComponent<Image>();
                if (fImg != null) fImg.color = new Color(0, 0, 0, 0);
                var vlg = filters.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) Object.DestroyImmediate(vlg);
                var csf = filters.GetComponent<ContentSizeFitter>();
                if (csf != null) Object.DestroyImmediate(csf);
                ChipGrid0926(filters);   // 터미널 / 기차가 두 칸 · 칸 너비는 실제 폭에 맞춘다
                SetRect(RT(filters), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, ChipsY), new Vector2(0, ChipH * 2 + ChipGap));
                RT(filters).offsetMin = new Vector2(SheetPad, RT(filters).offsetMin.y);
                RT(filters).offsetMax = new Vector2(-SheetPad, RT(filters).offsetMax.y);

                foreach (Transform chip in filters.transform) StyleChip(chip.gameObject);
            }

            // 집계 한 줄
            var summary = FindOrCreate(sheet.transform, "Summary0926");
            var sumT = Txt(summary, "", 34, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
            sumT.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(RT(summary), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, SummaryY), new Vector2(-SheetPad * 2, 48));

            // 목록
            var scroll = Find(panel.transform, "Scroll View");
            if (scroll != null)
            {
                scroll.transform.SetParent(sheet.transform, false);
                var srt = RT(scroll);
                srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 0.5f);
                srt.offsetMin = new Vector2(SheetPad - 8, ListBottom);
                srt.offsetMax = new Vector2(-(SheetPad - 8), ListTop);
                NoInset(scroll);   // 카드 자체가 안전영역만큼 올라간다

                var vp = Find(scroll.transform, "Viewport");
                if (vp != null)
                {
                    var vImg = vp.GetComponent<Image>();
                    if (vImg != null) vImg.color = new Color(0, 0, 0, 1);   // Mask 용 — showMaskGraphic false 로 안 보임
                    var mask = vp.GetComponent<Mask>();
                    if (mask != null) mask.showMaskGraphic = false;
                    var vrt = RT(vp); vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
                    vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
                }
                var sbar = Find(scroll.transform, "Scrollbar Vertical");
                if (sbar != null)
                {
                    var sr = scroll.GetComponent<ScrollRect>();
                    if (sr != null) { sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; EditorUtility.SetDirty(sr); }
                    var sbImg = sbar.GetComponent<Image>();
                    if (sbImg != null) sbImg.color = new Color(1, 1, 1, 0);
                    var handle = Find(sbar.transform, "Handle");
                    if (handle != null) Img(handle, Spr("r0926_pill"), new Color(1, 1, 1, 0.18f), Image.Type.Sliced, 64f / 4f);
                    SetRect(RT(sbar), new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(8, 0));
                }
                var content = Find(scroll.transform, "Content");
                var listText = content != null ? Find(content.transform, "ListText") : null;
                if (content != null)
                {
                    var crt = RT(content);
                    crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = new Vector2(0, crt.sizeDelta.y);
                    var vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
                    vlg.padding = new RectOffset(8, 8, 0, 0);
                    vlg.spacing = 0;
                    vlg.childControlWidth = true; vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

                    var tpl = BuildRowTemplate(content.transform);
                    var rows = Ensure<R0926PlaceRows>(content);
                    var so = new SerializedObject(rows);
                    so.FindProperty("source").objectReferenceValue = listText != null ? listText.GetComponent<Text>() : null;
                    so.FindProperty("rowContainer").objectReferenceValue = crt;
                    so.FindProperty("rowTemplate").objectReferenceValue = tpl;
                    so.FindProperty("countText").objectReferenceValue = count.GetComponent<Text>();
                    so.FindProperty("summaryText").objectReferenceValue = sumT;
                    so.FindProperty("barSprite").objectReferenceValue = Spr("r0926_pill");
                    so.FindProperty("rowHeight").floatValue = RowH;
                    so.FindProperty("directionWidth").floatValue = 160f;   // 방향 글자('북동')도 거리와 함께 키움
                    so.FindProperty("directionFontSize").intValue = 33;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (listText != null)
                {
                    // 데이터 없음 안내 등은 Summary 로 옮겨지지만, 원본도 읽기 좋게 맞춰 둔다
                    var lt = listText.GetComponent<Text>();
                    lt.fontSize = 40;
                    lt.lineSpacing = 1.1f;
                }
            }

            // 닫기 — 기존 XButton_List 를 도크의 '목록' 자리에 (누르면 그 자리에 X 가 생기는 원래 방식 유지)
            var close = Find(panel.transform, "XButton_List");
            if (close != null)
            {
                DockSlotClose(close, panel.transform, SlotList);
                // 바깥 누르기로는 닫지 않는다 (2026-10-04 대표님: X · 아래로 밀기로만) — 목록이 도크를 덮고 있어
                // 다른 도크 버튼 자리를 누르면 그 터치가 '바깥'으로 잡혀 목록이 닫혔다
                var tap = panel.GetComponent<R0926TapToClose>();
                if (tap != null) Object.DestroyImmediate(tap);
                if (pImg != null) pImg.raycastTarget = true;
            }

            // 예전 카드 배경은 숨긴다. 저작권 그림은 설정 › 앱 정보의 버전 아래로 옮겼다 (2026-10-23 대표님 — 목록은 아래 끝까지). Apply6 BuildSettingsPanel
            var copy = Find(back != null ? back.transform : sheet.transform, "copyright") ?? Find(sheet.transform, "copyright");
            if (back != null)
            {
                var bImg = back.GetComponent<Image>();
                if (bImg != null) bImg.enabled = false;
            }
            if (copy != null) Object.DestroyImmediate(copy);
            // 버전은 목록에서 빼고 설정 탭 맨 아래에 작게 (2026-10-06 대표님 — 목록의 버전 버튼 둘은 대표님이 씬에서 지웠다). Apply6 BuildSettingsPanel
            var info = panel.transform.Find("Info");
            if (info != null) info.gameObject.SetActive(false);
            log.Add("list sheet ok");
        }

        // (토글 이름, 종류, 색, 짧은 이름 ko/en/ja/zh/es)
        private static readonly (string name, R0926FilterChip.Kind kind, string hex, string[] words)[] ChipDefs =
        {
            ("CategoryToggle", R0926FilterChip.Kind.Category, "#E8EDF1", null),
            ("PetFriendlyToggle", R0926FilterChip.Kind.Pet, "#FFC107", new[] { "애견동반", "Pet-friendly", "ペット同伴", "可带宠物", "Con mascotas" }),   // 10-26 대표님 — 켜면 진한 노랑
            ("PublicDataToggle", R0926FilterChip.Kind.Simple, "#59B7FF", new[] { "공공데이터", "Public data", "公共データ", "公共数据", "Público" }),
            ("FestivalToggle", R0926FilterChip.Kind.Simple, "#FF6B57", new[] { "축제 / 보물", "Fests / Hunts", "祭り / 宝探し", "节庆 / 寻宝", "Fiesta / Tesoro" }),   // 10-27 대표님 '축제' → '축제 / 보물'
            ("MetroToggle", R0926FilterChip.Kind.Simple, "#3FC1B0", new[] { "지하철", "Subway", "地下鉄", "地铁", "Metro" }),
            // 한 칸이 되어 스페인어는 짧게 (Terminal / Tren 은 최소 글자로도 칸을 넘는다)
            ("TerminalToggle", R0926FilterChip.Kind.Simple, "#7DDC54", new[] { "터미널 / 기차", "Bus / Train", "ターミナル / 駅", "客运站 / 火车站", "Bus / Tren" }),
            ("3DObjectToggle", R0926FilterChip.Kind.Simple, "#8B5CF6", new[] { "3D", "3D", "3D", "3D", "3D" }),
            ("P2PUserToggle", R0926FilterChip.Kind.P2P, "#E95383", null),
        };

        // 칩 이름 앞 그림 (10-27 대표님 — 동그라미 대신 이모지 · 목록 줄과 같은 그림). 분류 칩은 CatIconKeys
        private static readonly Dictionary<string, string> ChipIconKeys = new Dictionary<string, string>
        {
            { "PetFriendlyToggle", "r0926_c_pet" }, { "PublicDataToggle", "r0926_c_gov" }, { "FestivalToggle", "r0926_f_party" },
            { "MetroToggle", "r0926_c_subway" }, { "TerminalToggle", "r0926_c_bus" }, { "3DObjectToggle", "r0926_c_3d" },
            { "P2PUserToggle", "r0926_i_user" },
        };

        private static void StyleChip(GameObject chip)
        {
            chip.transform.localScale = Vector3.one;
            var le = chip.GetComponent<LayoutElement>();
            if (le != null) { le.preferredWidth = -1; le.preferredHeight = -1; le.minWidth = -1; le.minHeight = -1; }

            var body = chip.GetComponent<Image>();
            if (body == null) body = chip.AddComponent<Image>();
            body.sprite = Spr("r0926_pill");
            body.type = Image.Type.Sliced;
            body.pixelsPerUnitMultiplier = 64f / 34f;   // 둥근 네모 (알약 아님)
            body.color = new Color(1, 1, 1, 0.07f);
            body.raycastTarget = true;

            // 체크 동그라미는 쓰지 않는다 — 칸 자체가 밝아지고 어두워진다 (토글의 graphic 참조는 그대로)
            var bg = Find(chip.transform, "Background");
            if (bg != null && bg.activeSelf) bg.SetActive(false);

            var dot = FindOrCreate(chip.transform, "Dot0926");
            var dImg = Img(dot, Spr("r0926_circle"), Color.white, Image.Type.Simple);
            dImg.raycastTarget = false;
            SetRect(RT(dot), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));

            Text t = null;
            var label = Find(chip.transform, "Label");
            if (label != null)
            {
                t = label.GetComponent<Text>();
                t.fontSize = 44;
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.resizeTextForBestFit = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = true;
                if (font != null) t.font = font;
                var lrt = RT(label);
                lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                EditorUtility.SetDirty(t);
            }

            // 반려견 '안 되는 곳만'의 빗금은 없앴다 (10-18 대표님 — 반려견은 두 가지)
            var strike = chip.transform.Find("Strike0926");
            if (strike != null) Object.DestroyImmediate(strike.gameObject);

            var def = System.Array.Find(ChipDefs, d => d.name == chip.name);
            if (def.name == null) return;
            // 분류 칩 — 누르면 고르기 목록이 펼쳐진다는 ∨ (10-17 대표님: 누를 때마다 다음 분류로 넘어가던 ↻ 대신)
            var cycle = chip.transform.Find("Cycle0926");
            if (cycle != null) Object.DestroyImmediate(cycle.gameObject);
            Image drop = null;
            if (def.kind == R0926FilterChip.Kind.Category)
            {
                var c = FindOrCreate(chip.transform, "Drop0926");
                drop = Img(c, Spr("r0926_i_down"), new Color(1, 1, 1, 0.75f), Image.Type.Simple);
                drop.raycastTarget = false;
                drop.preserveAspect = true;
                SetRect(RT(c), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            }
            var fc = Ensure<R0926FilterChip>(chip);
            var so = new SerializedObject(fc);
            so.FindProperty("kind").enumValueIndex = (int)def.kind;
            ColorUtility.TryParseHtmlString(def.hex, out var col);
            so.FindProperty("color").colorValue = col;
            var arr = so.FindProperty("names");
            arr.arraySize = def.words != null ? def.words.Length : 0;
            for (int i = 0; i < arr.arraySize; i++) arr.GetArrayElementAtIndex(i).stringValue = def.words[i];
            so.FindProperty("toggle").objectReferenceValue = chip.GetComponent<Toggle>();
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("label").objectReferenceValue = t;
            so.FindProperty("dot").objectReferenceValue = dImg;
            so.FindProperty("drop").objectReferenceValue = drop;
            so.FindProperty("solidOn").boolValue = chip.name == "3DObjectToggle";   // 켜지면 꽉 찬 보라 (10-23 대표님)
            so.FindProperty("modeChip").boolValue = chip.name == "FestivalToggle";
            so.FindProperty("icon").objectReferenceValue = ChipIconKeys.TryGetValue(chip.name, out var iconKey) ? Spr(iconKey) : null;
            so.FindProperty("tintIcon").boolValue = chip.name == "P2PUserToggle";   // 사람 모양은 한 색 — 칩 색(핑크)으로
            var icons = new Sprite[def.kind == R0926FilterChip.Kind.Category ? CatIconKeys.Length : 0];
            for (int i = 0; i < icons.Length; i++) icons[i] = Spr("r0926_c_" + CatIconKeys[i]);
            SetArray(so.FindProperty("catIcons"), icons);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildRowTemplate(Transform content)
        {
            var tpl = Find(content, "RowTemplate0926");
            if (tpl == null) tpl = NewUI("RowTemplate0926", content);
            tpl.SetActive(false);
            var le = Ensure<LayoutElement>(tpl);
            le.preferredHeight = RowH; le.minHeight = RowH;

            var line = FindOrCreate(tpl.transform, "Line");
            Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));

            var dot = FindOrCreate(tpl.transform, "Dot");
            Img(dot, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22, 0), new Vector2(26, 26));

            var name = FindOrCreate(tpl.transform, "Name");
            var nt = Txt(name, "장소", 48, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            nt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nt.verticalOverflow = VerticalWrapMode.Truncate;
            var nrt = RT(name);
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1); nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.offsetMin = new Vector2(60, 0); nrt.offsetMax = new Vector2(-236, 0);

            var dist = FindOrCreate(tpl.transform, "Dist");
            Txt(dist, "0m", 41, Muted, TextAnchor.MiddleRight, FontStyle.Normal);
            SetRect(RT(dist), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(226, RowH));
            return tpl;
        }

        // ============================================================
        // 스프라이트 가져오기 설정
        // ============================================================
        private static void EnsureSpriteImports(List<string> log)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Redesign0926/Sprites" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                Vector4 border = file == "r0926_pill" || file == "r0926_pill_ring" ? new Vector4(63, 63, 63, 63)
                               : file == "r0926_sheet" ? new Vector4(63, 1, 63, 63)
                               : file == "r0926_card" ? new Vector4(150, 150, 150, 150)
                               : Vector4.zero;
                bool dirty = ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled
                             || ti.spriteBorder != border || ti.textureCompression != TextureImporterCompression.Uncompressed
                             || ti.spriteImportMode != SpriteImportMode.Single;
                if (!dirty) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.spriteBorder = border;
                ti.SaveAndReimport();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                log.Add("import " + file);
            }
        }

        // ============================================================
        // 헬퍼
        // ============================================================
        private static Sprite Spr(string name)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
            if (sp == null) Debug.LogWarning("[Redesign0926] 스프라이트 없음: " + name);
            return sp;
        }

        private static RectTransform RT(GameObject go) => (RectTransform)go.transform;

        public static GameObject Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? t.gameObject : NewUI(name, parent);
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            EditorUtility.SetDirty(rt);
        }

        private static Image Img(GameObject go, Sprite sp, Color c, Image.Type type, float ppuMul = 1f)
        {
            var img = Ensure<Image>(go);
            img.sprite = sp;
            img.type = type;
            img.color = c;
            img.pixelsPerUnitMultiplier = ppuMul;
            EditorUtility.SetDirty(img);
            return img;
        }

        private static Text Txt(GameObject go, string text, float size, Color c, TextAnchor align, FontStyle style)
        {
            var t = Ensure<Text>(go);
            if (text != null) t.text = text;
            t.fontSize = Mathf.RoundToInt(size);
            t.color = c;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            EditorUtility.SetDirty(t);
            return t;
        }

        private static void Loc(GameObject go, string ko, string en, string ja, string zh, string es)
        {
            var l = Ensure<R0926LocalizedText>(go);
            l.ko = ko; l.en = en; l.ja = ja; l.zh = zh; l.es = es;
            EditorUtility.SetDirty(l);
        }
    }
}
