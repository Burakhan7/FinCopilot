import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fincopilot/core/theme/app_colors.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _phoneController = TextEditingController();
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = false;

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  String? _validatePhone(String? value) {
    if (value == null || value.isEmpty) return 'Telefon numarası gerekli.';
    final digits = value.replaceAll(RegExp(r'\D'), '');
    if (digits.length < 10) return 'Geçerli bir telefon numarası girin.';
    return null;
  }

  Future<void> _sendOtp() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isLoading = true);

    try {
      final phone = _phoneController.text.replaceAll(RegExp(r'\D'), '');
      // TODO: API çağrısı — POST /auth/register veya /auth/login
      // final result = await ref.read(authRepositoryProvider).sendOtp(phone);

      if (mounted) {
        context.goNamed('otp', extra: phone);
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
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SizedBox(height: 48),

                // Başlık
                Text(
                  'Merhaba!',
                  style: theme.textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  'Telefon numaranı girerek başla.',
                  style: theme.textTheme.bodyLarge?.copyWith(
                    color: AppColors.textSecondaryLight,
                  ),
                ),
                const SizedBox(height: 40),

                // Telefon numarası
                TextFormField(
                  controller: _phoneController,
                  keyboardType: TextInputType.phone,
                  inputFormatters: [
                    FilteringTextInputFormatter.allow(RegExp(r'[\d\s\-\+\(\)]')),
                    LengthLimitingTextInputFormatter(15),
                  ],
                  validator: _validatePhone,
                  decoration: const InputDecoration(
                    labelText: 'Telefon Numarası',
                    hintText: '05XX XXX XX XX',
                    prefixIcon: Icon(Icons.phone_outlined),
                    prefixText: '+90 ',
                  ),
                ),
                const SizedBox(height: 24),

                // Devam butonu
                ElevatedButton(
                  onPressed: _isLoading ? null : _sendOtp,
                  child: _isLoading
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('Devam'),
                ),

                const Spacer(),

                // Alt bilgi
                Center(
                  child: Text(
                    'Devam ederek kullanım koşullarını kabul etmiş olursunuz.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondaryLight,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
