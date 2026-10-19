import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:smooth_page_indicator/smooth_page_indicator.dart';
import 'package:fincopilot/core/storage/secure_storage.dart';
import 'package:fincopilot/core/theme/app_colors.dart';

class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  final _controller = PageController();
  int _currentPage = 0;

  static const _pages = [
    _OnboardingPage(
      icon: Icons.account_balance,
      title: 'Tüm Hesapların Tek Yerde',
      description:
          'Farklı bankalarındaki hesap, kart ve bakiyelerini '
          'tek ekrandan takip et.',
    ),
    _OnboardingPage(
      icon: Icons.insights,
      title: 'Akıllı Harcama Analizi',
      description:
          'Harcamalarını otomatik kategorize eder, aboneliklerini bulur, '
          'günlük harcama limitini hesaplar.',
    ),
    _OnboardingPage(
      icon: Icons.chat,
      title: 'AI Finans Koçun',
      description:
          'Doğal dilde sorular sor: "Bu ay markete ne harcadım?" '
          'Gerçek verilerine dayalı cevaplar al.',
    ),
  ];

  void _next() {
    if (_currentPage < _pages.length - 1) {
      _controller.nextPage(
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeInOut,
      );
    } else {
      _finish();
    }
  }

  Future<void> _finish() async {
    await ref.read(secureStorageProvider).setOnboardingDone();
    if (mounted) context.goNamed('login');
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              // Atla butonu
              Align(
                alignment: Alignment.topRight,
                child: TextButton(
                  onPressed: _finish,
                  child: const Text('Atla'),
                ),
              ),

              // Sayfalar
              Expanded(
                child: PageView.builder(
                  controller: _controller,
                  itemCount: _pages.length,
                  onPageChanged: (i) => setState(() => _currentPage = i),
                  itemBuilder: (context, i) => _buildPage(_pages[i], theme),
                ),
              ),

              // Indicator
              SmoothPageIndicator(
                controller: _controller,
                count: _pages.length,
                effect: WormEffect(
                  dotHeight: 8,
                  dotWidth: 8,
                  activeDotColor: theme.colorScheme.primary,
                  dotColor: theme.colorScheme.primary.withOpacity(0.2),
                ),
              ),
              const SizedBox(height: 32),

              // Devam butonu
              ElevatedButton(
                onPressed: _next,
                child: Text(
                  _currentPage == _pages.length - 1 ? 'Başla' : 'Devam',
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildPage(_OnboardingPage page, ThemeData theme) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Container(
          width: 120,
          height: 120,
          decoration: BoxDecoration(
            color: theme.colorScheme.primaryContainer,
            shape: BoxShape.circle,
          ),
          child: Icon(
            page.icon,
            size: 56,
            color: theme.colorScheme.primary,
          ),
        ),
        const SizedBox(height: 40),
        Text(
          page.title,
          style: theme.textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.bold,
          ),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 16),
        Text(
          page.description,
          style: theme.textTheme.bodyLarge?.copyWith(
            color: AppColors.textSecondaryLight,
            height: 1.5,
          ),
          textAlign: TextAlign.center,
        ),
      ],
    );
  }
}

class _OnboardingPage {
  final IconData icon;
  final String title;
  final String description;

  const _OnboardingPage({
    required this.icon,
    required this.title,
    required this.description,
  });
}
