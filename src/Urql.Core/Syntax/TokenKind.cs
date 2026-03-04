namespace Urql.Core.Syntax;

public enum TokenKind
{
    EndOfFile = 0,
    NewLine = 1,

    Identifier = 10,
    Number = 11,
    String = 12,

    Colon = 20,            // :
    Comma = 21,            // ,
    Ampersand = 22,        // &
    OpenParen = 23,        // (
    CloseParen = 24,       // )
    Plus = 25,             // +
    Minus = 26,            // -
    Star = 27,             // *
    Slash = 28,            // /
    Percent = 29,          // %
    Equals = 30,           // =
    DoubleEquals = 31,     // ==
    NotEquals = 32,        // <>
    Less = 33,             // <
    LessOrEquals = 34,     // <=
    Greater = 35,          // >
    GreaterOrEquals = 36,  // >=
    Hash = 37,             // #
    Dollar = 38,           // $
    Question = 39,         // ?

    KeywordIf = 100,
    KeywordThen = 101,
    KeywordElse = 102,
    KeywordGoto = 103,
    KeywordProc = 104,
    KeywordEnd = 105,
    KeywordInstr = 106,
    KeywordP = 107,
    KeywordPrint = 108,
    KeywordPln = 109,
    KeywordPrintln = 110,
    KeywordBtn = 111,
    KeywordAnd = 112,
    KeywordOr = 113,
    KeywordNot = 114
}
