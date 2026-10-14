"""ÖHVPS v2.0.0 Hesap Bilgisi rızası ve erişim belirteci şemaları.

Alan adları spesifikasyondaki JSON adlarıyla birebir aynıdır.
"""
from datetime import datetime
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

IZIN_TURLERI = {"01", "02", "03", "04", "05", "06", "07", "08", "09"}


class _Base(BaseModel):
    model_config = ConfigDict(extra="ignore")


# ---------- Rıza isteği ----------
class KatilimciBlg(_Base):
    hhsKod: str = Field(min_length=1, max_length=8)
    yosKod: str = Field(min_length=1, max_length=8)


class AyrikGkd(_Base):
    ohkTanimTip: Literal["TCKN", "MNO", "YKN", "PNO", "GSM", "IBAN"]
    ohkTanimDeger: str = Field(min_length=1, max_length=30)


class GkdIstek(_Base):
    yetYntm: Literal["Y", "A"]
    yonAdr: str = Field(min_length=1, max_length=500)
    ayrikGkd: AyrikGkd | None = None


class Kmlk(_Base):
    kmlkTur: Literal["K", "M", "Y", "P"]
    kmlkVrs: str = Field(min_length=1, max_length=30)
    krmKmlkTur: str | None = None
    krmKmlkVrs: str | None = None
    ohkTur: Literal["B", "K"]


class IznBlg(_Base):
    iznTur: list[str] = Field(min_length=1)
    erisimIzniSonTrh: datetime
    hesapIslemBslZmn: datetime | None = None
    hesapIslemBtsZmn: datetime | None = None


class HspBlgIstek(_Base):
    iznBlg: IznBlg


class HesapBilgisiRizasiIstegi(_Base):
    katilimciBlg: KatilimciBlg
    gkd: GkdIstek
    kmlk: Kmlk
    hspBlg: HspBlgIstek


# ---------- Rıza yanıtı ----------
class RzBlg(_Base):
    rizaNo: str
    olusZmn: datetime
    gnclZmn: datetime
    rizaDrm: Literal["B", "Y", "K", "S", "I"]
    rizaIptDtyKod: str | None = None


class KmlkYanit(_Base):
    kmlkTur: str
    kmlkVrs: str
    ohkTur: str


class GkdYanit(_Base):
    yetYntm: str
    yonAdr: str
    hhsYonAdr: str | None = None
    yetTmmZmn: datetime | None = None
    ayrikGkd: AyrikGkd | None = None


class HspBlgYanit(_Base):
    iznBlg: IznBlg


class HesapBilgisiRizasi(_Base):
    rzBlg: RzBlg
    kmlk: KmlkYanit
    katilimciBlg: KatilimciBlg
    gkd: GkdYanit
    hspBlg: HspBlgYanit


# ---------- Erişim belirteci ----------
class ErisimBelirteciIstegi(_Base):
    rizaNo: str
    rizaTip: Literal["O", "H", "I", "D"]
    yetTip: Literal["yet_kod", "yenileme_belirteci"]
    yetKod: str | None = None
    yenilemeBelirteci: str | None = None


class ErisimBelirteci(_Base):
    erisimBelirteci: str
    gecerlilikSuresi: int
    yenilemeBelirteci: str
    yenilemeBelirteciGecerlilikSuresi: int
