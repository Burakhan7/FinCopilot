import 'package:flutter/material.dart';

abstract final class AppColors {
  // Primary — koyu mavi-yeşil (finans, güven)
  static const Color primary = Color(0xFF0D6E6E);
  static const Color primaryLight = Color(0xFF4DA6A6);
  static const Color primaryDark = Color(0xFF094D4D);
  static const Color onPrimary = Color(0xFFFFFFFF);

  // Secondary — altın/amber (para, değer)
  static const Color secondary = Color(0xFFF5A623);
  static const Color secondaryLight = Color(0xFFFFCA61);
  static const Color onSecondary = Color(0xFF1A1A1A);

  // Surface & Background
  static const Color backgroundLight = Color(0xFFF8F9FA);
  static const Color surfaceLight = Color(0xFFFFFFFF);
  static const Color backgroundDark = Color(0xFF121212);
  static const Color surfaceDark = Color(0xFF1E1E1E);

  // Text
  static const Color textPrimaryLight = Color(0xFF1A1A2E);
  static const Color textSecondaryLight = Color(0xFF6B7280);
  static const Color textPrimaryDark = Color(0xFFF1F1F1);
  static const Color textSecondaryDark = Color(0xFF9CA3AF);

  // Semantic
  static const Color success = Color(0xFF22C55E);
  static const Color warning = Color(0xFFF59E0B);
  static const Color error = Color(0xFFEF4444);
  static const Color info = Color(0xFF3B82F6);

  // Income & Expense
  static const Color income = Color(0xFF22C55E);
  static const Color expense = Color(0xFFEF4444);

  // Category chip colors
  static const List<Color> categoryPalette = [
    Color(0xFF0D6E6E),
    Color(0xFFF5A623),
    Color(0xFF6366F1),
    Color(0xFFEC4899),
    Color(0xFF14B8A6),
    Color(0xFFF97316),
    Color(0xFF8B5CF6),
    Color(0xFF06B6D4),
  ];
}
