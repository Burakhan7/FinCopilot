import 'package:dio/dio.dart';
import 'package:fincopilot/core/constants/api_constants.dart';
import 'package:fincopilot/core/storage/secure_storage.dart';

class AuthInterceptor extends Interceptor {
  final Dio _dio;
  final SecureStorageService _storage;
  bool _isRefreshing = false;

  AuthInterceptor({
    required Dio dio,
    required SecureStorageService storage,
  })  : _dio = dio,
        _storage = storage;

  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    // Auth gerektirmeyen endpoint'ler
    final noAuthPaths = [
      ApiConstants.register,
      ApiConstants.verifyOtp,
      ApiConstants.login,
    ];

    if (noAuthPaths.contains(options.path)) {
      return handler.next(options);
    }

    final token = await _storage.getAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }

    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    if (err.response?.statusCode != 401 || _isRefreshing) {
      return handler.next(err);
    }

    _isRefreshing = true;

    try {
      final refreshToken = await _storage.getRefreshToken();
      if (refreshToken == null) {
        await _storage.clearAuth();
        return handler.next(err);
      }

      final response = await _dio.post(
        ApiConstants.refreshToken,
        data: {'refreshToken': refreshToken},
      );

      final newAccess = response.data['accessToken'] as String;
      final newRefresh = response.data['refreshToken'] as String;
      await _storage.setAccessToken(newAccess);
      await _storage.setRefreshToken(newRefresh);

      // Başarısız olan isteği yeni token ile tekrarla
      final opts = err.requestOptions;
      opts.headers['Authorization'] = 'Bearer $newAccess';
      final retryResponse = await _dio.fetch(opts);
      handler.resolve(retryResponse);
    } on DioException {
      await _storage.clearAuth();
      handler.next(err);
    } finally {
      _isRefreshing = false;
    }
  }
}
