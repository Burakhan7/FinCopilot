abstract final class ApiConstants {
  /// Core API base URL — Docker Compose'da "api" servis adı.
  /// Gerçek cihazda .env veya --dart-define ile override edilir.
  static const String baseUrl = 'http://10.0.2.2:5000/api';

  // Auth
  static const String register = '/auth/register';
  static const String verifyOtp = '/auth/verify-otp';
  static const String login = '/auth/login';
  static const String refreshToken = '/auth/refresh';

  // Consent (banka bağlama)
  static const String consents = '/consents';
  static String consentCallback(String id) => '/consents/$id/callback';

  // Accounts
  static const String accounts = '/accounts';
  static const String balances = '/accounts/balances';

  // Transactions
  static String transactions(String accountId) =>
      '/accounts/$accountId/transactions';

  // Cards
  static const String cards = '/cards';
  static String cardStatements(String cardId) => '/cards/$cardId/statements';
  static String cardTransactions(String cardId) =>
      '/cards/$cardId/transactions';

  // Dashboard
  static const String financialOverview = '/dashboard/overview';
  static const String spendingBreakdown = '/dashboard/spending';
  static const String safeToSpend = '/dashboard/safe-to-spend';

  // Chat
  static const String chat = '/chat';

  // Subscriptions
  static const String subscriptions = '/subscriptions';

  // Alerts
  static const String alerts = '/alerts';
}
