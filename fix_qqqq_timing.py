import re

file_path = r'c:\woopang\server\qqqq\templates\web_cmd_v2.html'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. setInterval 간격을 1500에서 300으로 변경
content = re.sub(r'setInterval\(poll, 1500\)', 'setInterval(poll, 300) // 300ms 폴링 (실시간 타이핑 효과)', content)

# 2. loadSessionHistory에 폰트 대기 추가
old_pattern = r'function loadSessionHistory\(\)\{ console\.log\(\'📜 세션 히스토리 로드:\', currentSid\); term\.innerHTML=\'\'; lastId=-1; fetch\(API_BASE\+\'/logs\?sid=\'\+currentSid\+\'&last_id=-1&t=\'\+Date\.now\(\)\)\.then\(function\(r\)\{return r\.json\(\)\}\)\.then\(function\(d\)\{ if\(d\.logs&&d\.logs\.length>0\)\{ var inc=\"\"; d\.logs\.forEach\(function\(l\)\{inc\+=l\.text; lastId=l\.id;\}\); render\(inc\); \} updateContextButtonStatus\(\); \}\); \}'

new_code = '''function loadSessionHistory(){
            console.log('📜 세션 히스토리 로드:', currentSid);
            term.innerHTML='';
            lastId=-1;
            // 폰트 로드 완료 후 히스토리 로드
            document.fonts.ready.then(function(){
                fetch(API_BASE+'/logs?sid='+currentSid+'&last_id=-1&t='+Date.now()).then(function(r){return r.json()}).then(function(d){
                    if(d.logs&&d.logs.length>0){
                        var inc="";
                        d.logs.forEach(function(l){inc+=l.text; lastId=l.id;});
                        render(inc);
                    }
                    updateContextButtonStatus();
                });
            });
        }'''

content = re.sub(old_pattern, new_code, content)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print('완료!')
