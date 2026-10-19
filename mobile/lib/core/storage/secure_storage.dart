import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

final secureStorageProvider = Provider<SecureStorageService>((ref) {
  return SecureStorageService();
});

class SecureStorageService {
  static const _storage = FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
  );

  static const _keyAccessToken = 'access_token';
  static const _keyRefreshToken = 'refresh_token';
  static const _keyPin = 'pin_hash';
  static const _keyOnboardingDone = 'onboarding_done';

  // Access Token
  Future<String?> getAccessToken() => _storage.read(key: _keyAccessToken);
  Future<void> setAccessToken(String token) =>
      _storage.write(key: _keyAccessToken, value: token);

  // Refresh Token
  Future<String?> getRefreshToken() => _storage.read(key: _keyRefreshToken);
  Future<void> setRefreshToken(String token) =>
      _storage.write(key: _keyRefreshToken, value: token);

  // PIN
  Future<String?> getPin() => _storage.read(key: _keyPin);
  Future<void> setPin(String pinHash) =>
      _storage.write(key: _keyPin, value: pinHash);
  Future<bool> hasPin() async => await _storage.read(key: _keyPin) != null;

  // Onboarding
  Future<bool> isOnboardingDone() async =>
      await _storage.read(key: _keyOnboardingDone) == 'true';
  Future<void> setOnboardingDone() =>
      _storage.write(key: _keyOnboardingDone, value: 'true');

  // Logout
  Future<void> clearAuth() async {
    await _storage.delete(key: _keyAccessToken);
    await _storage.delete(key: _keyRefreshToken);
  }

  Future<void> clearAll() => _storage.deleteAll();
}
