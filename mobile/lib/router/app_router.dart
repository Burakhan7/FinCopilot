import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fincopilot/core/constants/app_constants.dart';
import 'package:fincopilot/features/splash/splash_screen.dart';
import 'package:fincopilot/features/onboarding/onboarding_screen.dart';
import 'package:fincopilot/features/auth/presentation/screens/login_screen.dart';
import 'package:fincopilot/features/auth/presentation/screens/otp_screen.dart';
import 'package:fincopilot/features/auth/presentation/screens/pin_screen.dart';
import 'package:fincopilot/features/dashboard/presentation/screens/home_screen.dart';
import 'package:fincopilot/features/accounts/presentation/screens/accounts_screen.dart';
import 'package:fincopilot/features/consent/presentation/screens/bank_connect_screen.dart';
import 'package:fincopilot/features/chat/presentation/screens/chat_screen.dart';
import 'package:fincopilot/router/shell_screen.dart';

final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: '/',
    debugLogDiagnostics: true,
    routes: [
      // Splash
      GoRoute(
        path: '/',
        name: 'splash',
        builder: (context, state) => const SplashScreen(),
      ),

      // Onboarding
      GoRoute(
        path: '/onboarding',
        name: 'onboarding',
        builder: (context, state) => const OnboardingScreen(),
      ),

      // Auth
      GoRoute(
        path: '/login',
        name: 'login',
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: '/otp',
        name: 'otp',
        builder: (context, state) {
          final phone = state.extra as String? ?? '';
          return OtpScreen(phone: phone);
        },
      ),
      GoRoute(
        path: '/pin',
        name: 'pin',
        builder: (context, state) {
          final isSetup = state.extra as bool? ?? false;
          return PinScreen(isSetup: isSetup);
        },
      ),

      // Main app — bottom nav shell
      ShellRoute(
        builder: (context, state, child) => ShellScreen(child: child),
        routes: [
          GoRoute(
            path: '/home',
            name: 'home',
            builder: (context, state) => const HomeScreen(),
          ),
          GoRoute(
            path: '/accounts',
            name: 'accounts',
            builder: (context, state) => const AccountsScreen(),
          ),
          GoRoute(
            path: '/chat',
            name: 'chat',
            builder: (context, state) => const ChatScreen(),
          ),
        ],
      ),

      // Banka bağlama (modal)
      GoRoute(
        path: '/connect-bank',
        name: 'connectBank',
        builder: (context, state) => const BankConnectScreen(),
      ),
    ],

    // GKD deep link handler
    redirect: (context, state) {
      final uri = state.uri;
      // fincopilot://callback?yetKod=...&drmKod=...&rizaNo=...
      if (uri.scheme == AppConstants.deepLinkScheme &&
          uri.host == AppConstants.deepLinkHost) {
        // GKD callback'i — consent feature'ı handle eder
        return '/accounts?gkd=true'
            '&yetKod=${uri.queryParameters['yetKod']}'
            '&drmKod=${uri.queryParameters['drmKod']}'
            '&rizaNo=${uri.queryParameters['rizaNo']}';
      }
      return null;
    },
  );
});
