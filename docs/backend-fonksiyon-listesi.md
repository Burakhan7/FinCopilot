# Backend Fonksiyon Listesi (v1 taslak)

İlke: **LLM konuşur, backend hesaplar, kural motoru karar verir.** LLM'in çağırabildiği tool'lar sadece okuma yapar, hiçbir yere yazmaz.
Sütunlar: 🤖 = LLM tool olarak açık · ⏱ = arka plan işi (cron veya olay ile tetiklenir) · Faz: MVP / F2

## Veritabanı tabloları (fonksiyonların okuduğu yerler)
`consents` · `accounts` · `balance_snapshots` · `transactions` · `cards` · `card_statements` (kart-detay) · `card_transactions` · `merchants` · `categories` · `recurring_items` (abonelik, gelir, kira) · `installments` · `alerts` · `savings_events` (kazanç sayacı) · `user_settings` (faiz oranları, maaş günü, hedefler)

---

## 1. Genel Mali Tablo
| Fonksiyon | Ne yapar | Girdi | Okuduğu tablo | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|---|
| `get_financial_overview` | Net varlık özeti | user_id | balance_snapshots, card_statements, accounts | toplam nakit, kart borcu, KMH kullanımı, net varlık, kullanılabilir limit | 🤖 | MVP |
| `get_account_balances` | Banka banka bakiye | user_id, bank? | balance_snapshots, accounts | [{banka, hesap, bakiye, bakiye_zamanı}] | 🤖 | MVP |
| `get_debt_summary` | Şu anki borç durumu | user_id | card_statements, balances (krdHsp), installments | dönem borçları, asgari tutarlar, KMH borcu, kalan taksit toplamı | 🤖 | MVP |
| `get_future_obligations` | İleri borç takvimi | user_id, horizon_days | card_statements, installments, recurring_items | [{tarih, tür, açıklama, tutar}] + toplam | 🤖 | MVP |

## 2. Harcama Analizi
| Fonksiyon | Ne yapar | Girdi | Okuduğu tablo | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|---|
| `get_spending_breakdown` | Oransal dağılım | user_id, period, group_by (category/merchant/bank) | transactions, card_transactions | [{grup, tutar, yüzde, işlem_sayısı}] | 🤖 | MVP |
| `get_spending` | Filtreli harcama sorgusu | user_id, period, category?, merchant? | transactions, card_transactions | toplam, adet, ortalama, en büyük 5 işlem | 🤖 | MVP |
| `compare_periods` | Dönem karşılaştırma | user_id, period_a, period_b, category? | transactions | fark (TL ve %), en çok artan kategoriler | 🤖 | MVP |
| `get_inflation_adjusted_spending` | Reel harcama değişimi | user_id, category, period | transactions + TÜİK TÜFE | nominal %, enflasyon %, reel % | 🤖 | F2 |

## 3. Abonelikler
| Fonksiyon | Ne yapar | Girdi | Okuduğu tablo | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|---|
| `detect_recurring_items` | Tekrarlayan işlemleri bulur (abonelik, kira, aidat, maaş) | user_id | transactions, card_transactions → recurring_items'a yazar | – | ⏱ | MVP |
| `list_subscriptions` | Abonelik listesi | user_id | recurring_items | [{ad, tutar, periyot, sonraki_çekim, fiyat_artışı?}] + aylık toplam | 🤖 | MVP |

## 4. Kartlar ve Optimizasyon
| Fonksiyon | Ne yapar | Girdi | Okuduğu tablo | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|---|
| `get_card_cycles` | Kesim ve son ödeme sayacı | user_id | cards, card_statements | [{kart, kesim_tarihi, son_ödeme, kalan_gün, dönem_borcu, kalan_limit}] | 🤖 | MVP |
| `recommend_card` | Bugünkü harcama için en kârlı kart | user_id, amount, date=today | cards, card_statements | önerilen kart, faizsiz gün sayısı, gerekçe, limit yeterli mi | 🤖 | MVP |
| `plan_card_payment` | Kart ödemesi ne zaman, ne kadar, hangi hesaptan | user_id, card_ref | card_statements, balance_snapshots, user_settings | ödeme günü, tutar, kaynak hesap, "asgari ödersen ~X TL faiz" uyarısı | 🤖 | MVP |
| `get_installment_schedule` | 12 aylık taksit takvimi | user_id, months=12 | installments | [{ay, toplam_taksit, kart_kırılımı}] | 🤖 | MVP |

## 5. Tahmin ve Simülasyon
| Fonksiyon | Ne yapar | Girdi | Okuduğu tablo | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|---|
| `forecast_cashflow` | Günlük bakiye projeksiyonu | user_id, horizon_days | balances, recurring_items, card_statements, installments | [{tarih, beklenen_bakiye}], eksiye düşme tarihi | 🤖 | MVP |
| `get_safe_to_spend` | Günlük en fazla harcanabilir tutar | user_id, until=next_payday | forecast_cashflow çıktısı | günlük limit, bugüne kadar harcanan, kalan | 🤖 | MVP |
| `simulate_new_expense` | Yeni masrafın etkisi | user_id, amount, frequency (tek/aylık/taksit), start_date, installments? | forecast_cashflow + girdi | aylık etki, yeni günlük limit, riskli aylar, eksiye düşme var mı | 🤖 | MVP |
| `simulate_goal` | "X TL biriktirebilir miyim?" | user_id, target_amount, target_date | forecast_cashflow | mümkün mü, aylık gereken, önerilen kesinti kategorileri | 🤖 | F2 |

## 6. Para Kazandıran Özellikler
| Fonksiyon | Ne yapar | Girdi | Okuduğu tablo | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|---|
| `detect_kmh_usage` | KMH kurtarıcı | user_id | balance_snapshots (krdHsp), user_settings (faiz) | KMH'li hesap, kullanılan gün, tahmini faiz, kapatabilecek diğer hesap | 🤖⏱ | MVP |
| `get_bank_fees` | Banka masraf avcısı | user_id, period | transactions (islTur=UCRET_KOMISYON_FAIZ) | toplam, banka bazlı kırılım, kalemler | 🤖 | MVP |
| `detect_idle_cash` | Atıl para dedektörü | user_id, min_amount, min_days | balance_snapshots | [{hesap, tutar, atıl_gün}] | 🤖⏱ | F2 |
| `detect_duplicate_charges` | Mükerrer çekim ve iade takibi | user_id | card_transactions | şüpheli çiftler, bekleyen iadeler | ⏱ | F2 |
| `get_savings_counter` | "Uygulama sana ne kazandırdı" | user_id, period? | savings_events | toplam TL, kalem bazlı (faiz, gecikme, KMH, abonelik) | 🤖 | MVP |

## 7. Bildirim ve Alışkanlık
| Fonksiyon | Ne yapar | Tetik | Okuduğu tablo | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|---|
| `evaluate_alert_rules` | Son ödeme yaklaşıyor, bakiye yetersiz, eksiye düşecek, KMH uyarıları | cron (senkronizasyon sonrası) | tümü → alerts | alert kayıtları | ⏱ | MVP |
| `build_daily_brief` | Sabah brifingi | cron 08:00 | alerts, get_card_cycles, get_safe_to_spend | push metni verisi | ⏱ | MVP |
| `build_payday_plan` | Maaş günü planı | maaş işlemi (islAmc=05 / recurring income) | forecast_cashflow, get_future_obligations | ayrılan kalemler, serbest para, yeni günlük limit | ⏱ | MVP |
| `get_widget_data` | Ana ekran widget'ı | istemci isteği | get_safe_to_spend, get_card_cycles | günlük limit + en yakın son ödeme | – | MVP |

## 8. Skor ve Rapor
| Fonksiyon | Ne yapar | Girdi | Döndürür | 🤖 | Faz |
|---|---|---|---|---|---|
| `get_financial_health_score` | 0–100 finansal sağlık skoru | user_id | skor + alt metrikler (borç/gelir, birikim oranı, gecikme, KMH) | 🤖 | F2 |
| `build_monthly_report` | Aylık karne ve yıllık Wrapped | user_id, period | özet metrikler, paylaşılabilir kart verisi | – | F2 |

---

## Altyapı fonksiyonları (LLM'e açık değil)
| Modül | Fonksiyonlar |
|---|---|
| Rıza ve GKD | `create_consent`, `handle_gkd_callback` (drmKod doğrulama), `exchange_token`, `refresh_token`, `revoke_consent`, `check_consent_expiry` ⏱ |
| Senkronizasyon | `initial_backfill` (kullanıcı tetikli, 12 ay), `scheduled_sync` ⏱ (günde ≤4 çağrı, 24 saat penceresi), `handle_event_notification` (POST /olay-dinleme) |
| Zenginleştirme | `normalize_merchant`, `categorize_transaction` (islAmc/islTur → Regex → ML → LLM fallback + önbellek), `extract_installments` |
| Güvenlik | `mask_pii` (LLM'e giden her veride), `validate_llm_numbers` (cevaptaki rakamları tool çıktısıyla karşılaştırır) |

## Dışarıdan gerekecek veriler (API'de yok)
- Kredi kartı akdi/gecikme faizi ve KMH faizi: TCMB azami oranları varsayılan, kullanıcı kendi oranıyla düzeltebilir.
- TÜFE: TÜİK.
- Maaş günü: otomatik tespit, kullanıcı onaylar.
