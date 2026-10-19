/// Basit Result tipi — API çağrılarından dönen başarı/hata durumları.
sealed class Result<T> {
  const Result();

  bool get isSuccess => this is Success<T>;
  bool get isFailure => this is Failure<T>;

  T? get dataOrNull => switch (this) {
        Success(data: final d) => d,
        Failure() => null,
      };

  String? get errorOrNull => switch (this) {
        Success() => null,
        Failure(message: final m) => m,
      };

  R when<R>({
    required R Function(T data) success,
    required R Function(String message) failure,
  }) =>
      switch (this) {
        Success(data: final d) => success(d),
        Failure(message: final m) => failure(m),
      };
}

class Success<T> extends Result<T> {
  final T data;
  const Success(this.data);
}

class Failure<T> extends Result<T> {
  final String message;
  const Failure(this.message);
}
