import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:fincopilot/core/storage/secure_storage.dart';
import 'package:fincopilot/features/auth/presentation/screens/login_screen.dart';
import 'package:fincopilot/features/auth/presentation/screens/pin_screen.dart';
import 'package:fincopilot/features/onboarding/onboarding_screen.dart';
import 'package:fincopilot/main.dart';

/// Testte flutter_secure_storage'ın platform kanalı yok: bellek içi sahte depolama.
class FakeSecureStorage extends SecureStorageService {
  FakeSecureStorage({this.onboardingDone = false, this.accessToken, this.pin});

  final bool onboardingDone;
  final String? accessToken;
  final String? pin;

  @override
  Future<bool> isOnboardingDone() async => onboardingDone;

  @override
  Future<String?> getAccessToken() async => accessToken;

  @override
  Future<bool> hasPin() async => pin != null;
}

Future<void> pumpApp(WidgetTester tester, FakeSecureStorage storage) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [secureStorageProvider.overrideWithValue(storage)],
      child: const FinCopilotApp(),
    ),
  );
  // Açılış ekranı 1,5 sn bekleyip yönlendirir.
  await tester.pump(const Duration(milliseconds: 1600));
  await tester.pump(const Duration(milliseconds: 500));
}

void main() {
  testWidgets('Açılış ekranı uygulama adını gösterir', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [secureStorageProvider.overrideWithValue(FakeSecureStorage())],
        child: const FinCopilotApp(),
      ),
    );

    expect(find.text('FinCopilot'), findsOneWidget);
    expect(find.text('Akıllı Finans Koçun'), findsOneWidget);

    await tester.pump(const Duration(seconds: 2)); // bekleyen zamanlayıcıyı bitir
  });

  testWidgets('İlk açılışta tanıtım ekranına gider', (tester) async {
    await pumpApp(tester, FakeSecureStorage());

    expect(find.byType(OnboardingScreen), findsOneWidget);
  });

  testWidgets('Tanıtımı görmüş ama oturumu yoksa girişe gider', (tester) async {
    await pumpApp(tester, FakeSecureStorage(onboardingDone: true));

    expect(find.byType(LoginScreen), findsOneWidget);
  });

  testWidgets('Oturumu ve PIN\'i varsa PIN ekranına gider', (tester) async {
    await pumpApp(tester, FakeSecureStorage(onboardingDone: true, accessToken: 'token', pin: 'hash'));

    expect(find.byType(PinScreen), findsOneWidget);
  });
}
