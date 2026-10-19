abstract final class AppConstants {
  static const String appName = 'FinCopilot';
  static const String appTagline = 'Akıllı Finans Koçun';

  // Deep link scheme — GKD dönüşü için
  static const String deepLinkScheme = 'fincopilot';
  static const String deepLinkHost = 'callback';

  // Onboarding
  static const int onboardingPageCount = 3;

  // OTP
  static const int otpLength = 6;
  static const int otpTimeoutSeconds = 120;

  // PIN
  static const int pinLength = 6;

  // Pagination
  static const int defaultPageSize = 20;

  // Sync
  static const int maxSyncPerDay = 4;
}
