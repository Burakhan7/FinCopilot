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
