import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fincopilot/core/theme/app_colors.dart';

/// Banka seçim ve rıza oluşturma ekranı.
/// Akış: Banka seç → izinleri onayla → POST /consents → HHS GKD sayfasına yönlendir
class BankConnectScreen extends ConsumerStatefulWidget {
  const BankConnectScreen({super.key});

  @override
  ConsumerState<BankConnectScreen> createState() => _BankConnectScreenState();
}

class _BankConnectScreenState extends ConsumerState<BankConnectScreen> {
  String? _selectedBankCode;
  bool _isLoading = false;

  // Mock banka listesi — backend'den gelecek
  static const _mockBanks = [
    _Bank(code: 'BANK_A', name: 'Demo Banka A', icon: Icons.business),
    _Bank(code: 'BANK_B', name: 'Demo Banka B', icon: Icons.account_balance),
    _Bank(code: 'BANK_C', name: 'Demo Banka C', icon: Icons.corporate_fare),
  ];

  Future<void> _startConsent() async {
    if (_selectedBankCode == null) return;

    setState(() => _isLoading = true);

    try {
      // TODO: API çağrısı
      // 1. POST /consents {hhsKod: _selectedBankCode}
      // 2. Yanıttan hhsYonAdr al
      // 3. url_launcher ile GKD sayfasına yönlendir

      // Geçici: başarılı bağlantı simülasyonu
      await Future.delayed(const Duration(seconds: 1));

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Banka GKD sayfasına yönlendirilecek...')),
        );
        context.pop();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Hata: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Banka Bağla'),
        leading: IconButton(
          icon: const Icon(Icons.close),
          onPressed: () => context.pop(),
        ),
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Banka Seç',
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Hesap ve kart bilgilerini görüntülemek istediğin bankayı seç.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: AppColors.textSecondaryLight,
              ),
            ),
            const SizedBox(height: 16),

            // Banka listesi
            Expanded(
              child: ListView.separated(
                itemCount: _mockBanks.length,
                separatorBuilder: (_, __) => const SizedBox(height: 8),
                itemBuilder: (context, i) {
                  final bank = _mockBanks[i];
                  final isSelected = _selectedBankCode == bank.code;

                  return Card(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                      side: BorderSide(
                        color: isSelected
                            ? theme.colorScheme.primary
                            : Colors.grey.shade200,
                        width: isSelected ? 2 : 1,
                      ),
                    ),
                    child: ListTile(
                      contentPadding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 8,
                      ),
                      leading: CircleAvatar(
                        backgroundColor:
                            theme.colorScheme.primary.withOpacity(0.1),
                        child: Icon(bank.icon, color: theme.colorScheme.primary),
                      ),
                      title: Text(
                        bank.name,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      trailing: isSelected
                          ? Icon(Icons.check_circle,
                              color: theme.colorScheme.primary)
                          : null,
                      onTap: () =>
                          setState(() => _selectedBankCode = bank.code),
                    ),
                  );
                },
              ),
            ),

            // İzin bilgisi
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.info.withOpacity(0.08),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(
                children: [
                  const Icon(Icons.info_outline, color: AppColors.info, size: 20),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Banka şifren sadece bankanın kendi sayfasına girilir. '
                      'FinCopilot şifreni görmez.',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: AppColors.info,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),

            // Bağla butonu
            ElevatedButton(
              onPressed:
                  _selectedBankCode != null && !_isLoading ? _startConsent : null,
              child: _isLoading
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Text('Bankaya Git ve Onayla'),
            ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
  }
}

class _Bank {
  final String code;
  final String name;
  final IconData icon;

  const _Bank({required this.code, required this.name, required this.icon});
}
