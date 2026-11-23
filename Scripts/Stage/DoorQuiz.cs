using UnityEngine;

/// <summary>
/// 문에 표시할 중학교 1학년 수준의 수식.
/// Value는 수식을 계산한 결과이며, 문의 연산(+, -, ×, ÷)에 그대로 적용된다.
/// </summary>
public struct DoorQuiz
{
    public BonusType Type;
    public int Value;
    public string Text;
}

/// <summary>
/// 중1 교육 과정(정수의 덧셈·뺄셈, 일차방정식, 거듭제곱)의 가장 기초적인 문제로 문 수식을 만든다.
/// 달리면서 바로 암산할 수 있도록 연산은 한 번, 식에 나오는 수는 1 ~ 9의 양수만 쓴다.
/// (음수는 "2-5"처럼 계산 결과로만 나온다.)
/// 정답(Value)을 먼저 정한 뒤 그 값이 나오도록 식을 역으로 구성한다.
/// </summary>
public static class DoorQuizGenerator
{
    private const string Times = "×";
    private const string Divide = "÷";

    // 덧셈/뺄셈 문의 계산 결과 범위 (0 제외, 음수 가능)
    private const int MinSignedValue = -3;
    private const int MaxSignedValue = 5;

    // 곱셈/나눗셈 문의 계산 결과 범위
    private const int MinMultiplier = 2;
    private const int MaxMultiplier = 3;

    // 식에 등장하는 수의 최댓값
    private const int MaxOperand = 9;

    private struct Expression
    {
        // 문의 연산 기호 뒤에 붙는 부분 (예: "(2-5)", "x", "(6÷2)")
        public string Body;
        // 방정식처럼 아래 줄에 따로 표시할 조건 (없으면 null)
        public string Condition;
    }

    private delegate Expression ExpressionBuilder(int value);

    private static readonly ExpressionBuilder[] SignedBuilders =
    {
        Subtraction, Subtraction, LinearEquation, Power
    };

    private static readonly ExpressionBuilder[] MultiplierBuilders =
    {
        Subtraction, Division, LinearEquation
    };

    private static readonly ExpressionBuilder[] EasyBuilders =
    {
        Subtraction
    };

    public static DoorQuiz Create(BonusType type)
    {
        bool isMultiplier = type == BonusType.Product || type == BonusType.Division;

        int value = isMultiplier
            ? Random.Range(MinMultiplier, MaxMultiplier + 1)
            : RandomNonZero(MinSignedValue, MaxSignedValue);

        ExpressionBuilder[] builders = isMultiplier ? MultiplierBuilders : SignedBuilders;
        Expression expression = builders[Random.Range(0, builders.Length)](value);

        return new DoorQuiz { Type = type, Value = value, Text = BuildText(type, expression) };
    }

    /// <summary>
    /// 첫 번째 문용 쉬운 문제. 결과는 항상 1 ~ 3 이므로 러너가 늘어난다.
    /// </summary>
    public static DoorQuiz CreateEasy()
    {
        int value = Random.Range(1, 4);
        Expression expression = EasyBuilders[Random.Range(0, EasyBuilders.Length)](value);

        return new DoorQuiz { Type = BonusType.Addition, Value = value, Text = BuildText(BonusType.Addition, expression) };
    }

    private static string BuildText(BonusType type, Expression expression)
    {
        string text = GetOperatorSymbol(type) + expression.Body;

        if (expression.Condition != null)
            text += "\n<size=70%>" + expression.Condition + "</size>";

        return text;
    }

    private static string GetOperatorSymbol(BonusType type)
    {
        switch (type)
        {
            case BonusType.Difference: return "-";
            case BonusType.Product: return Times;
            case BonusType.Division: return Divide;
            default: return "+";
        }
    }

    #region 수식 생성

    // 양수끼리의 뺄셈 (결과가 음수일 수 있음): (2-5) = -3, (7-4) = 3
    private static Expression Subtraction(int value)
    {
        int minB = Mathf.Max(1, 1 - value);
        int maxB = Mathf.Min(MaxOperand, MaxOperand - value);
        int b = Random.Range(minB, maxB + 1);

        return Inline($"{value + b}-{b}");
    }

    // 한 번에 풀리는 일차방정식: x (x+3=5)
    private static Expression LinearEquation(int value)
    {
        int minB = Mathf.Max(1, 1 - value);
        int b = Random.Range(minB, minB + 4);

        return new Expression { Body = "x", Condition = $"x+{b}={value + b}" };
    }

    // 거듭제곱 (2², 2³ 만 사용): (2²-1) = 3
    private static Expression Power(int value)
    {
        int exponent = Random.value < 0.5f ? 2 : 3;
        int power = exponent == 2 ? 4 : 8;
        int c = power - value;

        if (c < 0 || c > MaxOperand)
            return Subtraction(value);

        string powerText = $"2<sup>{exponent}</sup>";
        return Inline(c == 0 ? powerText : $"{powerText}-{c}");
    }

    // 양수끼리의 나눗셈: (6÷2) = 3
    private static Expression Division(int value)
    {
        int divisor = Random.Range(2, 4);
        return Inline($"{value * divisor}{Divide}{divisor}");
    }

    #endregion

    #region 표기 도우미

    private static Expression Inline(string body)
    {
        return new Expression { Body = "(" + body + ")" };
    }

    // min ~ max(포함) 사이에서 0을 제외하고 고른다.
    private static int RandomNonZero(int min, int max)
    {
        int number;
        do
        {
            number = Random.Range(min, max + 1);
        } while (number == 0);

        return number;
    }

    #endregion
}
