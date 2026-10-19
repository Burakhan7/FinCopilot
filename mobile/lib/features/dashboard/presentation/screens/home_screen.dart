import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fincopilot/core/theme/app_colors.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('FinCopilot'),
        actions: [
          IconButton(
            icon: const Icon(Icons.notifications_outlined),
            onPressed: () {
              // TODO: Bildirimler ekranı
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          // TODO: Dashboard verilerini yenile
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // Günlük limit kartı
            _SafeToSpendCard(theme: theme),
            const SizedBox(height: 16),

            // Hızlı bakış kartları
            Row(
              children: [
                Expanded(
                  child: _QuickStatCard(
                    theme: theme,
                    icon: Icons.account_balance_wallet,
                    label: 'Toplam Bakiye',
                    value: '₺ --',
                    color: AppColors.primary,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _QuickStatCard(
                    theme: theme,
                    icon: Icons.credit_card,
                    label: 'Kart Borcu',
                    value: '₺ --',
                    color: AppColors.expense,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),

            // Banka bağla CTA
            _ConnectBankCard(
              theme: theme,
              onTap: () => context.pushNamed('connectBank'),
            ),
            const SizedBox(height: 24),

            // En yakın son ödeme
            _SectionHeader(theme: theme, title: 'Yaklaşan Ödemeler'),
            const SizedBox(height: 8),
            _EmptyStateCard(
              theme: theme,
              icon: Icons.event_note,
              message: 'Banka bağladığında ödemelerin burada görünecek.',
            ),
            const SizedBox(height: 24),

            // Son işlemler
            _SectionHeader(theme: theme, title: 'Son İşlemler'),
            const SizedBox(height: 8),
            _EmptyStateCard(
              theme: theme,
              icon: Icons.receipt_long,
              message: 'Henüz işlem yok.',
            ),
          ],
        ),
      ),
    );
  }
}

class _SafeToSpendCard extends StatelessWidget {
  final ThemeData theme;

  const _SafeToSpendCard({required this.theme});

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Günlük Harcama Limiti',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: AppColors.textSecondaryLight,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              '₺ --',
              style: theme.textTheme.headlineLarge?.copyWith(
                fontWeight: FontWeight.bold,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              'Maaş gününe kalan: -- gün',
              style: theme.textTheme.bodySmall?.copyWith(
                color: AppColors.textSecondaryLight,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _QuickStatCard extends StatelessWidget {
  final ThemeData theme;
  final IconData icon;
  final String label;
  final String value;
  final Color color;

  const _QuickStatCard({
    required this.theme,
    required this.icon,
    required this.label,
    required this.value,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: color, size: 24),
            const SizedBox(height: 12),
            Text(
              label,
              style: theme.textTheme.bodySmall?.copyWith(
                color: AppColors.textSecondaryLight,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              value,
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ConnectBankCard extends StatelessWidget {
  final ThemeData theme;
  final VoidCallback onTap;

  const _ConnectBankCard({required this.theme, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Card(
      color: AppColors.primary.withOpacity(0.05),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: AppColors.primary.withOpacity(0.1),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(
                  Icons.add_card,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Banka Bağla',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Hesap ve kartlarını görüntülemek için banka bağla.',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondaryLight,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: AppColors.primary),
            ],
          ),
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  final ThemeData theme;
  final String title;

  const _SectionHeader({required this.theme, required this.title});

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: theme.textTheme.titleMedium?.copyWith(
        fontWeight: FontWeight.bold,
      ),
    );
  }
}

class _EmptyStateCard extends StatelessWidget {
  final ThemeData theme;
  final IconData icon;
  final String message;

  const _EmptyStateCard({
    required this.theme,
    required this.icon,
    required this.message,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          children: [
            Icon(icon, size: 40, color: Colors.grey.shade400),
            const SizedBox(height: 12),
            Text(
              message,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: AppColors.textSecondaryLight,
              ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}
