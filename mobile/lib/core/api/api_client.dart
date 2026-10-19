import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fincopilot/core/constants/api_constants.dart';
import 'package:fincopilot/core/api/auth_interceptor.dart';
import 'package:fincopilot/core/storage/secure_storage.dart';

final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: ApiConstants.baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
    ),
  );

  final storage = ref.read(secureStorageProvider);
  dio.interceptors.addAll([
    AuthInterceptor(dio: dio, storage: storage),
    LogInterceptor(
      requestBody: true,
      responseBody: true,
      logPrint: (obj) {
        // TODO: değiştir — production'da logger kullan
        // ignore: avoid_print
        print(obj);
      },
    ),
  ]);

  return dio;
});

final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient(ref.read(dioProvider));
});

class ApiClient {
  final Dio _dio;

  ApiClient(this._dio);

  Future<Response<T>> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
  }) =>
      _dio.get(path, queryParameters: queryParameters);

  Future<Response<T>> post<T>(
    String path, {
    Object? data,
  }) =>
      _dio.post(path, data: data);

  Future<Response<T>> put<T>(
    String path, {
    Object? data,
  }) =>
      _dio.put(path, data: data);

  Future<Response<T>> delete<T>(String path) => _dio.delete(path);
}
