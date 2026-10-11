// SPDX-License-Identifier: MIT
// Exercise the actual vendored Tint parser, including its Emscripten fallback.
// Build and run with engine/toolchain/verify-tint-floats.ps1.

#include "src/tint/utils/strconv/parse_num.h"

#include <cmath>
#include <cstdio>
#include <limits>
#include <string_view>

using tint::strconv::ParseNumberError;
int checks = 0;
int failed = 0;

template <typename T>
void Success(std::string_view input, T expected) {
    checks++;
    auto result = tint::strconv::ParseNumber<T>(input);
    if (result != tint::Success || result.Get() != expected ||
        (expected == 0 && std::signbit(result.Get()) != std::signbit(expected))) {
        std::printf("FAIL success: [%.*s]\n", int(input.size()), input.data());
        failed++;
    }
}

template <typename T>
void Error(std::string_view input, ParseNumberError expected) {
    checks++;
    auto result = tint::strconv::ParseNumber<T>(input);
    if (result == tint::Success || result.Failure() != expected) {
        std::printf("FAIL error %d: [%.*s]\n", int(expected), int(input.size()), input.data());
        failed++;
    }
}

template <typename T>
void SharedBoundaries() {
    Success<T>("0", T(0));
    Success<T>("-0", T(-0.0));
    Success<T>("0e-9999", T(0));
    Success<T>("-0e-9999", T(-0.0));
    Success<T>("1.25", T(1.25));
    Success<T>("-1.25", T(-1.25));
    Success<T>(".5", T(.5));
    Success<T>("-.5", T(-.5));
    Success<T>("1.", T(1));
    Success<T>("1e+2", T(100));
    Success<T>("1e-2", T(.01));
    for (std::string_view input : {"", " ", " 1", "\t1", "\n1", "\r1", "\v1", "\f1",
                                  "+1", "+0", "+inf", "+nan", "1 ", "1\t", "1x", "1e",
                                  "1e+", "-", ".", "--1", "0x1p0", "-0X1p2", "infinite",
                                  "nan(a-b)", "nan(abc", "1,25"}) {
        Error<T>(input, ParseNumberError::kUnparsable);
    }
    Error<T>(std::string_view("1\0junk", 6), ParseNumberError::kUnparsable);
    Error<T>(std::string_view("\0", 1), ParseNumberError::kUnparsable);
    Error<T>("1e9999", ParseNumberError::kResultOutOfRange);
    Error<T>("-1e9999", ParseNumberError::kResultOutOfRange);
    Error<T>("1e-9999", ParseNumberError::kResultOutOfRange);
    Error<T>("-1e-9999", ParseNumberError::kResultOutOfRange);
    Error<T>("1e9999junk", ParseNumberError::kResultOutOfRange);
    for (std::string_view input : {"INF", "infinity", "-infinity"}) {
        checks++;
        auto result = tint::strconv::ParseNumber<T>(input);
        if (result != tint::Success || !std::isinf(result.Get()) ||
            std::signbit(result.Get()) != (input.front() == '-')) {
            std::printf("FAIL infinity: [%.*s]\n", int(input.size()), input.data());
            failed++;
        }
    }
    for (std::string_view input : {"nan", "NaN", "-nan", "nan()", "nan(abc_123)"}) {
        checks++;
        auto result = tint::strconv::ParseNumber<T>(input);
        if (result != tint::Success || !std::isnan(result.Get())) {
            std::printf("FAIL nan: [%.*s]\n", int(input.size()), input.data());
            failed++;
        }
    }
}

int main() {
    SharedBoundaries<float>();
    SharedBoundaries<double>();
    Success<float>("3.4028234663852886e38", std::numeric_limits<float>::max());
    Success<float>("1.1754943508222875e-38", std::numeric_limits<float>::min());
    Success<float>("1.401298464324817e-45", std::numeric_limits<float>::denorm_min());
    Success<float>("1e-45", std::numeric_limits<float>::denorm_min());
    Success<float>("7.006492321624086e-46", std::numeric_limits<float>::denorm_min());
    Error<float>("7.006492321624085e-46", ParseNumberError::kResultOutOfRange);
    Error<float>("1e-46", ParseNumberError::kResultOutOfRange);
    Error<float>("3.4028236e38", ParseNumberError::kResultOutOfRange);
    Success<double>("1.7976931348623157e308", std::numeric_limits<double>::max());
    Success<double>("2.2250738585072014e-308", std::numeric_limits<double>::min());
    Success<double>("4.9406564584124654e-324", std::numeric_limits<double>::denorm_min());
    Success<double>("5e-324", std::numeric_limits<double>::denorm_min());
    Success<double>("2.5e-324", std::numeric_limits<double>::denorm_min());
    Error<double>("2.4e-324", ParseNumberError::kResultOutOfRange);
    Error<double>("1e-324", ParseNumberError::kResultOutOfRange);
    Error<double>("2e308", ParseNumberError::kResultOutOfRange);
    Success<int>("-2147483648", std::numeric_limits<int>::min());
    Success<int>("2147483647", std::numeric_limits<int>::max());
    Error<int>("2147483648", ParseNumberError::kResultOutOfRange);
    Error<int>("+1", ParseNumberError::kUnparsable);
    Error<int>(" 1", ParseNumberError::kUnparsable);
    Error<int>("1.0", ParseNumberError::kUnparsable);
    Success<uint64_t>("18446744073709551615", std::numeric_limits<uint64_t>::max());
    Error<uint64_t>("18446744073709551616", ParseNumberError::kResultOutOfRange);
    Error<uint64_t>("-1", ParseNumberError::kUnparsable);
    std::printf("Emscripten parse_num: %d checks, %d failures\n", checks, failed);
    return failed ? 1 : 0;
}
