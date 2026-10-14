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
