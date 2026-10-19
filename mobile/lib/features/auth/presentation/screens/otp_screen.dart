import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:pin_code_fields/pin_code_fields.dart';
import 'package:fincopilot/core/constants/app_constants.dart';
import 'package:fincopilot/core/theme/app_colors.dart';

class OtpScreen extends ConsumerStatefulWidget {
  final String phone;

  const OtpScreen({super.key, required this.phone});

  @override
  ConsumerState<OtpScreen> createState() => _OtpScreenState();
}

class _OtpScreenState extends ConsumerState<OtpScreen> {
  final _otpController = TextEditingController();
  bool _isLoading = false;
  int _remainingSeconds = AppConstants.otpTimeoutSeconds;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _startTimer();
  }

  void _startTimer() {
    _remainingSeconds = AppConstants.otpTimeoutSeconds;
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (_remainingSeconds > 0) {
        setState(() => _remainingSeconds--);
      } else {
        timer.cancel();
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _otpController.dispose();
    super.dispose();
  }

  String get _formattedTime {
    final m = _remainingSeconds ~/ 60;
    final s = _remainingSeconds % 60;
    return '${m.toString().padLeft(2, '0')}:${s.toString().padLeft(2, '0')}';
  }

  Future<void> _verifyOtp(String code) async {
    if (code.length != AppConstants.otpLength) return;

    setState(() => _isLoading = true);

    try {
      // TODO: API çağrısı — POST /auth/verify-otp
      // await ref.read(authRepositoryProvider).verifyOtp(widget.phone, code);

      if (mounted) {
        context.goNamed('pin', extra: true); // PIN oluşturma
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Doğrulama başarısız: $e')),
        );
        _otpController.clear();
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _resendOtp() async {
    // TODO: API çağrısı — tekrar OTP gönder
    _startTimer();
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Yeni kod gönderildi.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        leading: BackButton(onPressed: () => context.goNamed('login')),
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Doğrulama Kodu',
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                '+90 ${widget.phone} numarasına gönderilen '
                '${AppConstants.otpLength} haneli kodu gir.',
                style: theme.textTheme.bodyLarge?.copyWith(
                  color: AppColors.textSecondaryLight,
                ),
              ),
              const SizedBox(height: 32),

              // OTP alanı
              PinCodeTextField(
                appContext: context,
                controller: _otpController,
                length: AppConstants.otpLength,
                keyboardType: TextInputType.number,
                animationType: AnimationType.fade,
                pinTheme: PinTheme(
                  shape: PinCodeFieldShape.box,
                  borderRadius: BorderRadius.circular(12),
                  fieldHeight: 56,
                  fieldWidth: 48,
                  activeFillColor: theme.colorScheme.surface,
                  inactiveFillColor: theme.colorScheme.surface,
                  selectedFillColor: theme.colorScheme.surface,
                  activeColor: theme.colorScheme.primary,
                  inactiveColor: Colors.grey.shade300,
                  selectedColor: theme.colorScheme.primary,
                ),
                enableActiveFill: true,
                onCompleted: _verifyOtp,
                onChanged: (_) {},
              ),
              const SizedBox(height: 16),

              // Sayaç + tekrar gönder
              Center(
                child: _remainingSeconds > 0
                    ? Text(
                        'Tekrar gönder ($_formattedTime)',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: AppColors.textSecondaryLight,
                        ),
                      )
                    : TextButton(
                        onPressed: _resendOtp,
                        child: const Text('Tekrar Gönder'),
                      ),
              ),

              if (_isLoading) ...[
                const SizedBox(height: 24),
                const Center(child: CircularProgressIndicator()),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
