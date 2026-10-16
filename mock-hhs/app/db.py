"""SQLite tabanlı mock durum deposu (rıza, yetki kodu, token, GKD oturumu)."""
from datetime import datetime

from sqlalchemy import JSON, Boolean, DateTime, ForeignKey, String, create_engine
from sqlalchemy.orm import DeclarativeBase, Mapped, Session, mapped_column, sessionmaker

from . import config


class Base(DeclarativeBase):
    pass


class Consent(Base):
    __tablename__ = "consents"

    riza_no: Mapped[str] = mapped_column(String(64), primary_key=True)
    hhs_kod: Mapped[str] = mapped_column(String(8), index=True)
    yos_kod: Mapped[str] = mapped_column(String(8))
    kmlk_tur: Mapped[str] = mapped_column(String(1))
    kmlk_vrs: Mapped[str] = mapped_column(String(30), index=True)
    ohk_tur: Mapped[str] = mapped_column(String(1))
    izin_tur: Mapped[list] = mapped_column(JSON)
    erisim_izni_son_trh: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    hesap_isl_bsl_zmn: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    hesap_isl_bts_zmn: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    yet_yntm: Mapped[str] = mapped_column(String(1))
    yon_adr: Mapped[str] = mapped_column(String(500))
    ayrik_gkd: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    riza_drm: Mapped[str] = mapped_column(String(1), index=True)
    riza_ipt_dty_kod: Mapped[str | None] = mapped_column(String(2), nullable=True)
    olus_zmn: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    gncl_zmn: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    gkd_son_zmn: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    yet_tmm_zmn: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    secili_hesaplar: Mapped[list] = mapped_column(JSON, default=list)
    secili_kartlar: Mapped[list] = mapped_column(JSON, default=list)


class AuthCode(Base):
    __tablename__ = "auth_codes"

    yet_kod: Mapped[str] = mapped_column(String(64), primary_key=True)
    riza_no: Mapped[str] = mapped_column(ForeignKey("consents.riza_no"), index=True)
    expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    used: Mapped[bool] = mapped_column(Boolean, default=False)


class Token(Base):
    __tablename__ = "tokens"

    access_token: Mapped[str] = mapped_column(String(128), primary_key=True)
    riza_no: Mapped[str] = mapped_column(ForeignKey("consents.riza_no"), index=True)
    refresh_token: Mapped[str] = mapped_column(String(128), index=True)
    access_expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    refresh_expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))


class GkdSession(Base):
    """Sahte banka sayfasındaki adımlar arası durum (giriş → SMS → seçim)."""
    __tablename__ = "gkd_sessions"

    sid: Mapped[str] = mapped_column(String(64), primary_key=True)
    riza_no: Mapped[str] = mapped_column(ForeignKey("consents.riza_no"))
    drm_kod: Mapped[str | None] = mapped_column(String(128), nullable=True)
    step: Mapped[str] = mapped_column(String(16))  # login | otp | select
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))


class QueryLog(Base):
    """Sistem tetikli (PSU-Initiated=H) başarılı sorgular — günlük kota için."""
    __tablename__ = "query_log"

    id: Mapped[int] = mapped_column(primary_key=True, autoincrement=True)
    riza_no: Mapped[str] = mapped_column(String(64), index=True)
    resource: Mapped[str] = mapped_column(String(200))
    day: Mapped[str] = mapped_column(String(10), index=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))


class Subscription(Base):
    """YÖS'ün bir HHS'deki olay aboneliği (/olay-abonelik)."""
    __tablename__ = "subscriptions"

    olay_abonelik_no: Mapped[str] = mapped_column(String(64), primary_key=True)
    hhs_kod: Mapped[str] = mapped_column(String(8), index=True)
    yos_kod: Mapped[str] = mapped_column(String(8), index=True)
    abonelik_tipleri: Mapped[list] = mapped_column(JSON)   # [{"olayTipi":..,"kaynakTipi":..}]
    olus_zmn: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    gncl_zmn: Mapped[datetime] = mapped_column(DateTime(timezone=True))


class Event(Base):
    """Gönderilecek / gönderilmiş olaylar (outbox)."""
    __tablename__ = "events"

    olay_no: Mapped[str] = mapped_column(String(64), primary_key=True)
    olay_abonelik_no: Mapped[str] = mapped_column(String(64), index=True)
    hhs_kod: Mapped[str] = mapped_column(String(8))
    yos_kod: Mapped[str] = mapped_column(String(8))
    olay_tipi: Mapped[str] = mapped_column(String(40))
    kaynak_tipi: Mapped[str] = mapped_column(String(40))
    kaynak_no: Mapped[str] = mapped_column(String(64))
    olay_zamani: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    status: Mapped[str] = mapped_column(String(16), index=True)  # bekliyor | iletildi | iletilemedi
    attempts: Mapped[int] = mapped_column(default=0)
    next_attempt_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    last_error: Mapped[str | None] = mapped_column(String(300), nullable=True)


class ScanState(Base):
    """Rıza başına son taranan an (yeni hareket → BAKIYE olayı)."""
    __tablename__ = "scan_state"

    riza_no: Mapped[str] = mapped_column(String(64), primary_key=True)
    last_scan: Mapped[datetime] = mapped_column(DateTime(timezone=True))


class InjectedTx(Base):
    """Demo için elle eklenen hesap hareketleri (sentetik verinin üstüne bindirilir)."""
    __tablename__ = "injected_txs"

    id: Mapped[int] = mapped_column(primary_key=True, autoincrement=True)
    tckn: Mapped[str] = mapped_column(String(11), index=True)
    hsp_ref: Mapped[str] = mapped_column(String(40), index=True)
    zaman: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    tutar: Mapped[str] = mapped_column(String(24))
    brc_alc: Mapped[str] = mapped_column(String(1))
    isl_tur: Mapped[str] = mapped_column(String(40))
    isl_amc: Mapped[str | None] = mapped_column(String(2), nullable=True)
    aciklama: Mapped[str] = mapped_column(String(300))
    karsi_unvan: Mapped[str | None] = mapped_column(String(140), nullable=True)


class AppSession(Base):
    """Sahte banka mobil uygulaması oturumu (ayrık GKD)."""
    __tablename__ = "app_sessions"

    sid: Mapped[str] = mapped_column(String(64), primary_key=True)
    tckn: Mapped[str] = mapped_column(String(11))
    hhs_kod: Mapped[str] = mapped_column(String(8))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))


_connect_args = {"check_same_thread": False} if config.DATABASE_URL.startswith("sqlite") else {}
engine = create_engine(config.DATABASE_URL, connect_args=_connect_args)
SessionLocal = sessionmaker(bind=engine, expire_on_commit=False)


def init_db() -> None:
    Base.metadata.create_all(engine)


def get_db():
    db: Session = SessionLocal()
    try:
        yield db
    finally:
        db.close()
