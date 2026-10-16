"""Mock HHS ayarları. Hepsi ortam değişkeniyle değiştirilebilir."""
import os
from datetime import timedelta, timezone

TR_TZ = timezone(timedelta(hours=3))

DATABASE_URL = os.getenv("MOCK_HHS_DATABASE_URL", "sqlite:///./mock_hhs.db")

# Sahte GKD sayfasının dışarıdan erişilen adresi (mobil cihaz buraya yönlenir)
PUBLIC_BASE_URL = os.getenv("MOCK_HHS_PUBLIC_BASE_URL", "http://localhost:8090").rstrip("/")

# Tanımlı YÖS kodları (bizim uygulamamız). Virgülle ayrılmış.
ALLOWED_YOS_CODES = {c.strip() for c in os.getenv("MOCK_HHS_YOS_CODES", "8001").split(",") if c.strip()}

# GKD için tek kullanımlık SMS kodu (simülasyon)
TEST_OTP = os.getenv("MOCK_HHS_TEST_OTP", "123456")

# Süreler
GKD_TIMEOUT = timedelta(minutes=int(os.getenv("MOCK_HHS_GKD_TIMEOUT_MIN", "5")))          # B -> I(04)
YETKOD_TIMEOUT = timedelta(minutes=int(os.getenv("MOCK_HHS_YETKOD_TIMEOUT_MIN", "5")))    # Y -> I(05)
ACCESS_TOKEN_TTL = timedelta(seconds=int(os.getenv("MOCK_HHS_ACCESS_TOKEN_TTL_SEC", str(24 * 3600))))

# Rıza süre sınırları (ÖHVPS: bireysel en fazla 6 ay, kurumsal 12 ay, en az 1 gün)
MAX_CONSENT_DAYS = {"B": 183, "K": 366}
MIN_CONSENT = timedelta(days=1)
MAX_TX_HISTORY_DAYS = 366  # işlem sorgu aralığı geçmiş/gelecek en fazla 12 ay

# Hesap bilgisi sorgu kuralları (ÖHVPS)
PSU_WINDOW = {"B": timedelta(days=31), "K": timedelta(days=7)}   # ÖHK tetikli (PSU-Initiated=E)
SYSTEM_WINDOW = timedelta(hours=24)                               # sistem tetikli (PSU-Initiated=H)
SYSTEM_DAILY_LIMIT = {"B": 4}                                     # bireysel: günde 4 başarılı sorgu
SYSTEM_HOURLY_LIMIT = {"K": 12}                                   # kurumsal: saatte 12
MAX_PAGE_SIZE = int(os.getenv("MOCK_HHS_MAX_PAGE_SIZE", "100"))

# Olay bildirimi
# YÖS'ün olay dinleme adresi. Varsayılan: mock içindeki test dinleyicisi.
YOS_EVENT_URL = os.getenv("MOCK_HHS_YOS_EVENT_URL", f"{PUBLIC_BASE_URL}/_mock/yos/olay-dinleme")
EVENT_LOOP_ENABLED = os.getenv("MOCK_HHS_EVENT_LOOP", "1") == "1"
EVENT_TICK_SEC = float(os.getenv("MOCK_HHS_EVENT_TICK_SEC", "5"))
# Yeniden deneme (ÖHVPS): standart olaylar 30 dk aralıkla 3 deneme, kart ve GKD olayları 1 dk aralıkla 3 deneme.
# Demo için ölçeklenebilir: 0.01 → 18 sn / 0.6 sn
EVENT_RETRY_SCALE = float(os.getenv("MOCK_HHS_EVENT_RETRY_SCALE", "1"))
EVENT_RETRY = {"standart": timedelta(minutes=30), "hizli": timedelta(minutes=1)}
EVENT_MAX_ATTEMPTS = 3
