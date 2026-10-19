import 'package:dio/dio.dart';

class ApiException implements Exception {
  final String message;
  final int? statusCode;
  final dynamic data;

  const ApiException({
    required this.message,
    this.statusCode,
    this.data,
  });

  factory ApiException.fromDioException(DioException e) {
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return const ApiException(message: 'Bağlantı zaman aşımına uğradı.');
      case DioExceptionType.connectionError:
        return const ApiException(message: 'İnternet bağlantısı bulunamadı.');
      case DioExceptionType.badResponse:
        final statusCode = e.response?.statusCode;
        final data = e.response?.data;
        String message;

        if (data is Map<String, dynamic> && data.containsKey('message')) {
          message = data['message'] as String;
        } else {
          message = switch (statusCode) {
            400 => 'Geçersiz istek.',
            401 => 'Oturum süresi doldu. Lütfen tekrar giriş yapın.',
            403 => 'Bu işlem için yetkiniz yok.',
            404 => 'Kaynak bulunamadı.',
            409 => 'Çakışma: bu işlem zaten yapılmış.',
            422 => 'Gönderilen veri doğrulanamadı.',
            429 => 'Çok fazla istek. Lütfen biraz bekleyin.',
            500 => 'Sunucu hatası. Lütfen daha sonra tekrar deneyin.',
            _ => 'Beklenmeyen bir hata oluştu ($statusCode).',
          };
        }

        return ApiException(
          message: message,
          statusCode: statusCode,
          data: data,
        );
      default:
        return ApiException(message: e.message ?? 'Bilinmeyen hata.');
    }
  }

  @override
  String toString() => 'ApiException($statusCode): $message';
}
