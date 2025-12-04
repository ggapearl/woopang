import requests
import time
import logging
import os
import psutil
import subprocess
import smtplib
from email.mime.text import MIMEText
from datetime import datetime, timedelta
import json
import threading
import sys
import urllib3
from single_server_restart import SingleServerRestart
import colorama
from colorama import Fore, Back, Style, init

# Color initialization
init(autoreset=True)
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

class ColoredFormatter(logging.Formatter):
    """Colored log formatter"""
    
    COLORS = {
        'DEBUG': Fore.CYAN,
        'INFO': Fore.GREEN,
        'WARNING': Fore.YELLOW,
        'ERROR': Fore.RED,
        'CRITICAL': Fore.RED + Back.YELLOW
    }
    
    KEYWORD_COLORS = {
        '✅': Fore.GREEN,
        '❌': Fore.RED,
        '⚠️': Fore.YELLOW,
        '🚨': Fore.RED + Style.BRIGHT,
        '🎉': Fore.GREEN + Style.BRIGHT,
        '🔄': Fore.CYAN,
        '🚀': Fore.BLUE + Style.BRIGHT,
        '⏰': Fore.YELLOW,
        '🔧': Fore.MAGENTA,
        '📊': Fore.BLUE,
        '🎯': Fore.GREEN + Style.BRIGHT,
        '💤': Fore.BLUE,
        '⏳': Fore.CYAN,
        '🔒': Fore.MAGENTA,
        '🌐': Fore.CYAN
    }
    
    def format(self, record):
        log_message = super().format(record)
        level_color = self.COLORS.get(record.levelname, '')
        if level_color:
            log_message = f"{level_color}{log_message}{Style.RESET_ALL}"
        
        for keyword, color in self.KEYWORD_COLORS.items():
            if keyword in log_message:
                log_message = log_message.replace(keyword, f"{color}{keyword}{Style.RESET_ALL}")
        
        return log_message

# Logging setup - prevent duplicates
logger = logging.getLogger(__name__)
logger.setLevel(logging.INFO)

# Remove existing handlers
for handler in logger.handlers[:]:
    logger.removeHandler(handler)

# Custom formatter that removes INFO level from console
class ConsoleColoredFormatter(ColoredFormatter):
    def format(self, record):
        # INFO 레벨일 때는 levelname 제거
        if record.levelname == 'INFO':
            original_fmt = self._style._fmt
            self._style._fmt = '%(asctime)s - %(message)s'
            result = super().format(record)
            self._style._fmt = original_fmt
            return result
        else:
            # ERROR, WARNING 등은 levelname 포함
            return super().format(record)

# Console handler
console_handler = logging.StreamHandler()
console_handler.setLevel(logging.INFO)
console_formatter = ConsoleColoredFormatter('%(asctime)s - %(levelname)s - %(message)s')
console_handler.setFormatter(console_formatter)

# File handler
file_handler = logging.FileHandler('monitor.log', encoding='utf-8')
file_handler.setLevel(logging.INFO)
file_formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
file_handler.setFormatter(file_formatter)

logger.addHandler(console_handler)
logger.addHandler(file_handler)

# Prevent logger propagation
logger.propagate = False

class SmartMonitoringSystem:
    def __init__(self):
        # Basic settings - 외부 접속 체크로 변경
        self.main_url = "https://woopang.com"
        self.health_url = "https://woopang.com/health"
        self.check_interval = 10
        self.fast_check_interval = 2
        self.fast_check_attempts = 3
        
        # Timeout settings
        self.http_timeout = 8
        self.response_time_threshold = 15.0
        
        # Restart settings
        self.restart_attempts = 0
        self.max_restart_attempts = 3
        self.last_restart_time = None
        
        # Server status tracking
        self.main_server_status = "unknown"
        
        # Failure counters - 🔧 수정: 더 빠른 재시작을 위해 2로 변경
        self.main_consecutive_failures = 0
        self.max_consecutive_failures = 2  # 🔧 3에서 2로 변경
        
        # Status tracking
        self.last_success_time = datetime.now()
        self.last_health_data = None
        self.main_process = None
        
        # Statistics
        self.stats = {
            'total_checks': 0,
            'main_server_failures': 0,
            'successful_restarts': 0,
            'failed_restarts': 0,
            'uptime_start': datetime.now(),
            'connection_errors': 0,
            'timeout_errors': 0,
            'ssl_errors': 0
        }
        
        # Restart manager
        self.restart_manager = SingleServerRestart()
        
        # Thread control
        self.monitoring_active = True
        self.restart_in_progress = False
        
    def check_main_server(self):
        """Check main server status - 외부 접속 체크"""
        try:
            start_time = time.time()
            
            # 🔧 수정: 더 현실적인 User-Agent와 헤더 추가
            headers = {
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36',
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
                'Accept-Language': 'ko-KR,ko;q=0.8,en-US;q=0.5,en;q=0.3',
                'Accept-Encoding': 'gzip, deflate',
                'Connection': 'keep-alive'
            }
            
            response = requests.get(
                self.main_url, 
                timeout=self.http_timeout,
                verify=True,  # SSL 검증 활성화 (실제 환경과 동일)
                headers=headers,  # 🔧 수정: 개선된 헤더 사용
                allow_redirects=True
            )
            response_time = time.time() - start_time
            
            if response.status_code == 200:
                self.main_server_status = "healthy"
                self.main_consecutive_failures = 0
                return True, response_time
            else:
                self.main_server_status = "unhealthy"
                self.main_consecutive_failures += 1
                logger.warning(f"⚠️ HTTP Status {response.status_code} from {self.main_url}")
                return False, response_time
                
        except requests.exceptions.SSLError as e:
            self.main_server_status = "ssl_error"
            self.main_consecutive_failures += 1
            self.stats['ssl_errors'] += 1
            logger.error(f"🔒 SSL Error: {str(e)[:100]}...")
            return False, None
            
        except requests.exceptions.Timeout:
            self.main_server_status = "timeout"
            self.main_consecutive_failures += 1
            self.stats['timeout_errors'] += 1
            logger.warning(f"⏰ Timeout accessing {self.main_url}")
            return False, None
            
        except requests.exceptions.ConnectionError as e:
            self.main_server_status = "connection_error"
            self.main_consecutive_failures += 1
            self.stats['connection_errors'] += 1
            logger.warning(f"🔌 Connection Error: {str(e)[:100]}...")
            return False, None
            
        except Exception as e:
            self.main_server_status = "unknown_error"
            self.main_consecutive_failures += 1
            logger.error(f"❌ Unknown Error: {str(e)[:100]}...")
            return False, None
    
    def check_health_endpoint(self):
        """헬스체크 엔드포인트 추가 확인"""
        try:
            response = requests.get(
                self.health_url,
                timeout=5,
                verify=True,
                headers={'User-Agent': 'WoopangMonitor/Health'}
            )
            return response.status_code == 200, response.status_code
        except Exception:
            return False, None
    
    def comprehensive_server_check(self):
        """Comprehensive server status check"""
        main_healthy, main_time = self.check_main_server()
        health_healthy, health_status = self.check_health_endpoint()
        
        health_data = {
            'main_server': {
                'status': self.main_server_status,
                'healthy': main_healthy,
                'response_time': main_time,
                'consecutive_failures': self.main_consecutive_failures
            },
            'health_endpoint': {
                'healthy': health_healthy,
                'status_code': health_status
            },
            'overall_status': 'healthy' if main_healthy else 'unhealthy',
            'issues': [],
            'timestamp': datetime.now()
        }
        
        # 상태별 이슈 추가
        if self.main_server_status == "ssl_error":
            health_data['issues'].append('SSL_CERTIFICATE_ERROR')
        elif self.main_server_status == "connection_error":
            health_data['issues'].append('CONNECTION_REFUSED')
        elif self.main_server_status == "timeout":
            health_data['issues'].append('REQUEST_TIMEOUT')
        
        # Consecutive failure warnings
        if self.main_consecutive_failures >= self.max_consecutive_failures:
            health_data['issues'].append(f'CONSECUTIVE_FAILURES({self.main_consecutive_failures})')
        
        # System resource check
        try:
            memory_usage = psutil.virtual_memory().percent
            cpu_usage = psutil.cpu_percent(interval=0.1)
            
            health_data['system'] = {
                'memory_usage': memory_usage,
                'cpu_usage': cpu_usage
            }
            
            if memory_usage > 90:
                health_data['issues'].append(f'HIGH_MEMORY({memory_usage:.1f}%)')
            if cpu_usage > 95:
                health_data['issues'].append(f'HIGH_CPU({cpu_usage:.1f}%)')
                
        except Exception:
            health_data['issues'].append('SYSTEM_CHECK_FAILED')
        
        self.last_health_data = health_data
        return health_data
    
    def log_monitoring_summary(self):
        """Monitoring summary"""
        if self.last_health_data:
            overall_status = self.last_health_data['overall_status']
            main_healthy = self.last_health_data['main_server']['healthy']
            
            # Status message
            if overall_status == 'healthy':
                response_time = self.last_health_data['main_server']['response_time']

                if response_time:
                    logger.info(f"✅ Woopang.com healthy ({response_time:.2f}s) ✅")
                else:
                    logger.info(f"✅ Woopang.com healthy ✅")
            else:
                status_detail = self.main_server_status.upper().replace('_', ' ')
                logger.error(f"🚨 External Access FAILED - woopang.com {status_detail}")
                
                # 구체적인 문제 제시
                if self.main_server_status == "ssl_error":
                    logger.error("🔒 SSL certificate issue detected")
                elif self.main_server_status == "connection_error":
                    logger.error("🔌 Cannot connect to server")
                elif self.main_server_status == "timeout":
                    logger.error("⏰ Server response timeout")
    
    def fast_main_server_check(self):
        """Fast main server check"""
        logger.warning(f"🚨 External access issue detected! Fast checking ({self.fast_check_interval}s × {self.fast_check_attempts} attempts)...")
        
        for attempt in range(self.fast_check_attempts):
            try:
                start_time = time.time()
                
                # 🔧 수정: 빠른 체크에서도 개선된 헤더 사용
                headers = {
                    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
                    'Accept': 'text/html,application/xhtml+xml'
                }
                
                response = requests.get(
                    self.main_url, 
                    timeout=3,  # 빠른 체크용 짧은 타임아웃
                    verify=True,
                    headers=headers
                )
                response_time = time.time() - start_time
                
                if response.status_code == 200:
                    logger.info(f"✅ External access recovered! (attempt {attempt+1}/{self.fast_check_attempts}, {response_time:.2f}s)")
                    self.main_server_status = "healthy"
                    self.main_consecutive_failures = 0
                    return True
                else:
                    logger.warning(f"⚠️ Status {response.status_code} (attempt {attempt+1}/{self.fast_check_attempts})")
                    
            except requests.exceptions.SSLError as e:
                logger.warning(f"🔒 SSL Error (attempt {attempt+1}/{self.fast_check_attempts}): {str(e)[:50]}...")
            except requests.exceptions.ConnectionError as e:
                logger.warning(f"🔌 Connection Error (attempt {attempt+1}/{self.fast_check_attempts}): {str(e)[:50]}...")
            except requests.exceptions.Timeout:
                logger.warning(f"⏰ Timeout (attempt {attempt+1}/{self.fast_check_attempts})")
            except Exception as e:
                error_msg = str(e)[:50] + "..." if len(str(e)) > 50 else str(e)
                logger.warning(f"❌ Fast check failed (attempt {attempt+1}/{self.fast_check_attempts}): {error_msg}")
            
            if attempt < self.fast_check_attempts - 1:
                time.sleep(self.fast_check_interval)
        
        logger.error(f"🚨 External access confirmed down! ({self.fast_check_attempts} fast checks completed)")
        return False
    
    def kill_all_server_processes(self):
        """🔧 추가: 모든 서버 프로세스와 포트 점유 해제 - 중복 실행 방지"""
        try:
            logger.info("🔧 모든 서버 프로세스 정리 시작...")
            
            # 1. 포트 443을 사용하는 모든 프로세스 종료
            try:
                result = subprocess.run(['netstat', '-ano'], capture_output=True, text=True)
                for line in result.stdout.split('\n'):
                    if ':443 ' in line and 'LISTENING' in line:
                        pid = line.strip().split()[-1]
                        try:
                            subprocess.run(['taskkill', '/F', '/PID', pid], check=True, capture_output=True)
                            logger.info(f"✅ 포트 443 점유 프로세스 종료: PID {pid}")
                        except:
                            pass
            except Exception as e:
                logger.warning(f"⚠️ 포트 정리 실패: {e}")
            
            # 2. Python 프로세스 중 app_improved.py 실행 중인 것 모두 종료
            for proc in psutil.process_iter(['pid', 'name', 'cmdline']):
                try:
                    cmdline = proc.info['cmdline']
                    if cmdline and any('app_improved.py' in str(cmd) for cmd in cmdline):
                        psutil.Process(proc.info['pid']).terminate()
                        logger.info(f"✅ app_improved.py 프로세스 종료: PID {proc.info['pid']}")
                except:
                    pass
            
            # 3. 잠시 대기 후 강제 종료
            time.sleep(3)
            for proc in psutil.process_iter(['pid', 'name', 'cmdline']):
                try:
                    cmdline = proc.info['cmdline']
                    if cmdline and any('app_improved.py' in str(cmd) for cmd in cmdline):
                        psutil.Process(proc.info['pid']).kill()
                        logger.info(f"🔥 app_improved.py 프로세스 강제 종료: PID {proc.info['pid']}")
                except:
                    pass
                    
            logger.info("✅ 서버 프로세스 정리 완료")
            time.sleep(5)  # 포트 해제 대기
            
        except Exception as e:
            logger.error(f"❌ 프로세스 정리 실패: {e}")
    
    def restart_main_server(self):
        """Restart main server - 🔧 수정: 프로세스 정리 강화"""
        try:
            logger.info("🚀 Attempting to restart main server...")
            
            # 🔧 수정: 기존 restart_manager.kill_port_processes 대신 강화된 프로세스 정리 사용
            self.kill_all_server_processes()
            
            # Set environment variables (normal mode)
            env = os.environ.copy()
            env.pop('BACKUP_MODE', None)
            env.pop('FORCE_HTTP_PORT', None)
            
            logger.info("🔧 Main server environment cleared")
            
            # Start main server
            self.main_process = subprocess.Popen([
                "python", "app_improved.py"
            ], cwd="C:/woopang/server", 
            env=env,
            creationflags=subprocess.CREATE_NEW_CONSOLE)
            
            logger.info(f"📋 Main server process started - PID: {self.main_process.pid}")
            
            # Wait for main server startup (90 seconds for external access)
            for i in range(90):
                main_healthy, _ = self.check_main_server()
                if main_healthy:
                    logger.info(f"🎉 Main server restarted successfully - External access restored ({i+1}s)")
                    logger.info(f"🌐 External access: https://woopang.com")
                    self.stats['successful_restarts'] += 1
                    self.restart_attempts = 0
                    self.main_consecutive_failures = 0
                    self.last_restart_time = datetime.now()
                    return True
                    
                # Check if process died
                if self.main_process.poll() is not None:
                    logger.error("❌ Main server process terminated during startup")
                    break
                    
                time.sleep(1)
                if i % 15 == 14:
                    logger.info(f"⏳ Waiting for external access... ({i+1}/90)")
                    
            logger.error("❌ Main server restart failed (90s timeout)")
            self.stats['failed_restarts'] += 1
            return False
            
        except Exception as e:
            logger.error(f"❌ Failed to restart main server: {e}")
            self.stats['failed_restarts'] += 1
            return False
    
    def perform_server_restart(self):
        """Server restart process"""
        if self.restart_in_progress:
            logger.warning("🔄 Restart already in progress, skipping...")
            return False
            
        self.restart_in_progress = True
        self.restart_attempts += 1
        start_time = datetime.now()
        
        try:
            logger.info(f"🎯 Starting server restart (attempt {self.restart_attempts}/{self.max_restart_attempts})")
            
            # 1. Re-check current status
            health_data = self.comprehensive_server_check()
            
            if health_data['main_server']['healthy']:
                logger.info("✅ External access is actually working, no restart needed")
                self.restart_in_progress = False  # 🔧 수정: 플래그 즉시 해제
                return True
            
            # 2. Attempt main server restart
            logger.info("🚀 Starting main server restart...")
            restart_success = self.restart_main_server()
            
            if restart_success:
                logger.info("🎉 Main server restart successful!")
                self.last_success_time = datetime.now()
                
                end_time = datetime.now()
                duration = (end_time - start_time).total_seconds()
                
                logger.info(f"🎉 Server restart completed successfully! (Total time: {duration:.1f}s)")
                logger.info("🌐 External service restored at: https://woopang.com")
                
                return True
            else:
                logger.error("❌ Main server restart failed")
                return False
                
        except Exception as e:
            logger.error(f"❌ Server restart failed: {e}")
            return False
        finally:
            self.restart_in_progress = False
    
    def print_comprehensive_status(self):
        """Comprehensive status report"""
        uptime = datetime.now() - self.stats['uptime_start']
        
        print(f"\n{Fore.CYAN}{'='*70}{Style.RESET_ALL}")
        print(f"{Fore.BLUE + Style.BRIGHT}🚀 WOOPANG SERVER MONITORING STATUS (EXTERNAL MODE){Style.RESET_ALL}")
        print(f"{Fore.CYAN}{'='*70}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}⏰ Monitor uptime: {Fore.CYAN}{uptime}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}🔍 Total checks: {Fore.CYAN}{self.stats['total_checks']}{Style.RESET_ALL}")
        
        # Process information
        if self.main_process:
            print(f"{Fore.WHITE}📋 Main server PID: {Fore.CYAN}{self.main_process.pid}{Style.RESET_ALL}")
        
        print(f"{Fore.WHITE}🔄 External access failures: {Fore.RED}{self.stats['main_server_failures']}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}✅ Successful restarts: {Fore.GREEN}{self.stats['successful_restarts']}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}❌ Failed restarts: {Fore.RED}{self.stats['failed_restarts']}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}🔌 Connection errors: {Fore.RED}{self.stats['connection_errors']}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}⏰ Timeout errors: {Fore.RED}{self.stats['timeout_errors']}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}🔒 SSL errors: {Fore.RED}{self.stats['ssl_errors']}{Style.RESET_ALL}")
        print(f"{Fore.WHITE}🕐 Last success: {Fore.GREEN}{self.last_success_time.strftime('%H:%M:%S')}{Style.RESET_ALL}")
        if self.last_restart_time:
            print(f"{Fore.WHITE}🔧 Last restart: {Fore.CYAN}{self.last_restart_time.strftime('%H:%M:%S')}{Style.RESET_ALL}")
        
        # 🔧 수정: 재시작 조건 표시 추가
        print(f"{Fore.WHITE}🚨 Consecutive failures: {Fore.RED if self.main_consecutive_failures >= self.max_consecutive_failures else Fore.YELLOW}{self.main_consecutive_failures}/{self.max_consecutive_failures}{Style.RESET_ALL}")
        
        # Current server status
        if self.last_health_data:
            print(f"\n{Fore.WHITE}📊 Current External Access Status:{Style.RESET_ALL}")
            
            main_status = self.last_health_data['main_server']
            status_icon = f"{Fore.GREEN}✅{Style.RESET_ALL}" if main_status['healthy'] else f"{Fore.RED}❌{Style.RESET_ALL}"
            response_time = f" ({main_status['response_time']:.2f}s)" if main_status['response_time'] else ""
            failures = f" [consecutive failures: {main_status['consecutive_failures']}]" if main_status['consecutive_failures'] > 0 else ""
            
            # 상태별 색상 표시
            status_color = Fore.GREEN if main_status['healthy'] else Fore.RED
            status_text = main_status['status'].upper().replace('_', ' ')
            
            print(f"  {status_icon} woopang.com: {status_color}{status_text}{Style.RESET_ALL}{response_time}{Fore.RED if main_status['consecutive_failures'] > 0 else ''}{failures}{Style.RESET_ALL}")
            
            # Health endpoint status
            health_status = self.last_health_data['health_endpoint']
            health_icon = f"{Fore.GREEN}✅{Style.RESET_ALL}" if health_status['healthy'] else f"{Fore.YELLOW}⚠️{Style.RESET_ALL}"
            health_code = f" (HTTP {health_status['status_code']})" if health_status['status_code'] else ""
            print(f"  {health_icon} Health endpoint: {health_code}")
            
            overall_status = self.last_health_data['overall_status']
            if overall_status == 'healthy':
                print(f"  {Fore.GREEN}🎯 Overall: HEALTHY (External access working){Style.RESET_ALL}")
            else:
                print(f"  {Fore.RED}🚨 Overall: UNHEALTHY (External access failed){Style.RESET_ALL}")
            
            # System resources
            if 'system' in self.last_health_data:
                system = self.last_health_data['system']
                mem_color = Fore.RED if system['memory_usage'] > 90 else Fore.YELLOW if system['memory_usage'] > 75 else Fore.GREEN
                cpu_color = Fore.RED if system['cpu_usage'] > 90 else Fore.YELLOW if system['cpu_usage'] > 75 else Fore.GREEN
                print(f"  {Fore.WHITE}💾 Memory: {mem_color}{system['memory_usage']:.1f}%{Style.RESET_ALL}")
                print(f"  {Fore.WHITE}⚡ CPU: {cpu_color}{system['cpu_usage']:.1f}%{Style.RESET_ALL}")
            
            # Issues
            if self.last_health_data['issues']:
                issues_colored = []
                for issue in self.last_health_data['issues']:
                    if any(keyword in issue for keyword in ['SSL', 'CONNECTION', 'TIMEOUT']):
                        issues_colored.append(f"{Fore.RED}{issue}{Style.RESET_ALL}")
                    elif 'HIGH' in issue or 'CONSECUTIVE' in issue:
                        issues_colored.append(f"{Fore.YELLOW}{issue}{Style.RESET_ALL}")
                    else:
                        issues_colored.append(f"{Fore.WHITE}{issue}{Style.RESET_ALL}")
                print(f"  {Fore.RED}⚠️ Issues: {', '.join(issues_colored)}")
            else:
                print(f"  {Fore.GREEN}✅ No critical issues{Style.RESET_ALL}")
                
        print(f"{Fore.CYAN}{'='*70}{Style.RESET_ALL}")
    
    def run_monitoring(self):
        """Main monitoring loop"""
        logger.info(f"📊 Monitor initialized - External access check interval: {self.check_interval}s")
        logger.info(f"⚡ Fast check configuration: {self.fast_check_interval}s × {self.fast_check_attempts} attempts")
        logger.info(f"🎯 Strategy: External domain access monitoring (woopang.com)")
        logger.info(f"🔧 HTTP timeout: {self.http_timeout}s")
        logger.info(f"🔒 SSL verification: ENABLED (production mode)")
        logger.info(f"🚨 Restart trigger: {self.max_consecutive_failures} consecutive failures")  # 🔧 추가: 재시작 조건 표시
        logger.info(f"🚀 Monitoring system started successfully")
        
        try:
            while self.monitoring_active:
                self.stats['total_checks'] += 1
                
                # Comprehensive server status check
                health_data = self.comprehensive_server_check()
                
                # Regular summary log
                if self.stats['total_checks'] % 1 == 0:
                    self.log_monitoring_summary()
                
                # Status-based processing
                if health_data['overall_status'] == 'healthy':
                    self.last_success_time = datetime.now()
                    
                elif health_data['overall_status'] == 'unhealthy':
                    # External access down
                    logger.warning("⚠️ External access down!")
                    self.stats['main_server_failures'] += 1
                    
                    # 🔧 수정: 재시작 조건 체크 즉시 수행
                    if self.main_consecutive_failures >= self.max_consecutive_failures:
                        logger.error(f"🚨 재시작 조건 충족! (연속 {self.main_consecutive_failures}회 실패)")
                        
                        # Fast external access re-check
                        main_recovered = self.fast_main_server_check()
                        
                        if not main_recovered:
                            logger.error("🚨 External access confirmed down! Starting restart...")
                            
                            # Start restart thread
                            restart_thread = threading.Thread(
                                target=self.perform_server_restart,
                                daemon=True
                            )
                            restart_thread.start()
                            restart_thread.join(timeout=300)  # 5 minute timeout
                            
                        else:
                            logger.info("✅ External access recovered during fast check")
                    else:
                        logger.warning(f"⚠️ 연속 실패 {self.main_consecutive_failures}/{self.max_consecutive_failures} - 재시작 대기 중")
                
                # Periodic detailed report (every 5 minutes)
                if self.stats['total_checks'] % 30 == 0:
                    self.print_comprehensive_status()
                
                # Restart attempt limit check
                if self.restart_attempts >= self.max_restart_attempts:
                    logger.error(f"🚨 Maximum restart attempts exceeded ({self.max_restart_attempts})")
                    logger.warning("⏳ Waiting 5 minutes before resetting restart counter")
                    time.sleep(300)
                    self.restart_attempts = 0
                    logger.info("🔄 Restart counter reset completed")
                
                # Wait
                time.sleep(self.check_interval)
                
        except KeyboardInterrupt:
            logger.info("👋 Monitoring stopped by user")
        except Exception as e:
            logger.error(f"❌ Monitoring system crashed: {e}")
        finally:
            self.monitoring_active = False
            logger.info("📝 Monitoring system terminated")

if __name__ == "__main__":
    # 바로 모니터링 시작 (프롬프트 없이)
    monitor = SmartMonitoringSystem()
    monitor.run_monitoring()