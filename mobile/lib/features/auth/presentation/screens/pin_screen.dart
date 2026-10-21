import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:pin_code_fields/pin_code_fields.dart';
import 'package:fincopilot/core/constants/app_constants.dart';
import 'package:fincopilot/core/storage/secure_storage.dart';
import 'package:fincopilot/core/theme/app_colors.dart';

class PinScreen extends ConsumerStatefulWidget {
  /// true = ilk kez PIN oluşturma, false = kilit açma
  final bool isSetup;

  const PinScreen({super.key, required this.isSetup});

  @override
  ConsumerState<PinScreen> createState() => _PinScreenState();
}

class _PinScreenState extends ConsumerState<PinScreen> {
  final _pinController = TextEditingController();
  String? _firstPin; // setup modunda ilk giriş
  String? _errorText;
  bool _isConfirmStep = false;

  @override
  void dispose() {
    _pinController.dispose();
    super.dispose();
  }

  Future<void> _onCompleted(String pin) async {
    final storage = ref.read(secureStorageProvider);

    if (widget.isSetup) {
      // PIN oluşturma — iki kez girilmeli
      if (!_isConfirmStep) {
        setState(() {
          _firstPin = pin;
          _isConfirmStep = true;
          _errorText = null;
        });
        _pinController.clear();
        return;
      }

      // Onay
      if (pin != _firstPin) {
        setState(() {
          _errorText = 'PIN eşleşmedi. Tekrar dene.';
          _isConfirmStep = false;
          _firstPin = null;
        });
        _pinController.clear();
        HapticFeedback.heavyImpact();
        return;
      }

      // Kaydet
      // TODO: Hash'le — production'da bcrypt/argon2 kullan
      await storage.setPin(pin);
      if (mounted) context.goNamed('home');
    } else {
      // Kilit açma
      final stored = await storage.getPin();
      if (pin == stored) {
        if (mounted) context.goNamed('home');
      } else {
        setState(() => _errorText = 'Yanlış PIN.');
        _pinController.clear();
        HapticFeedback.heavyImpact();
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    String title;
    String subtitle;

    if (widget.isSetup) {
      if (_isConfirmStep) {
        title = 'PIN Onayla';
        subtitle = 'Aynı PIN\'i tekrar gir.';
      } else {
        title = 'PIN Oluştur';
        subtitle = '${AppConstants.pinLength} haneli bir PIN belirle.';
      }
    } else {
      title = 'Hoş Geldin';
      subtitle = 'Devam etmek için PIN\'ini gir.';
    }

    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              const SizedBox(height: 80),

              // Logo
              Container(
                width: 72,
                height: 72,
                decoration: BoxDecoration(
                  color: theme.colorScheme.primaryContainer,
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Icon(
                  Icons.lock_outlined,
                  size: 36,
                  color: theme.colorScheme.primary,
                ),
              ),
              const SizedBox(height: 32),

              Text(
                title,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                subtitle,
                style: theme.textTheme.bodyLarge?.copyWith(
                  color: AppColors.textSecondaryLight,
                ),
              ),
              const SizedBox(height: 40),

              // PIN alanı
              SizedBox(
                width: 280,
                child: PinCodeTextField(
                  appContext: context,
                  controller: _pinController,
                  autoDisposeControllers: false, // controller'ı ekran dispose ediyor
                  length: AppConstants.pinLength,
                  keyboardType: TextInputType.number,
                  obscureText: true,
                  obscuringCharacter: '●',
                  animationType: AnimationType.scale,
                  pinTheme: PinTheme(
                    shape: PinCodeFieldShape.circle,
                    fieldHeight: 20,
                    fieldWidth: 20,
                    activeFillColor: theme.colorScheme.primary,
                    inactiveFillColor: Colors.grey.shade300,
                    selectedFillColor: theme.colorScheme.primary.withOpacity(0.5),
                    activeColor: Colors.transparent,
                    inactiveColor: Colors.transparent,
                    selectedColor: Colors.transparent,
                  ),
                  enableActiveFill: true,
                  onCompleted: _onCompleted,
                  onChanged: (_) {
                    if (_errorText != null) setState(() => _errorText = null);
                  },
                ),
              ),

              if (_errorText != null) ...[
                const SizedBox(height: 16),
                Text(
                  _errorText!,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: AppColors.error,
                  ),
                ),
              ],

              // TODO: Biyometrik kilit açma butonu (isSetup == false iken)
            ],
          ),
        ),
      ),
    );
  }
}
