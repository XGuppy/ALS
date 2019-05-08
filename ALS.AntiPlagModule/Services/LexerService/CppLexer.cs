﻿using Antlr4.Runtime;
using ALS.AntiPlagModule.Constants;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ALS.AntiPlagModule.Services.LexerService
{
    public class CppLexer : ILexer
    {
        public string[] KeyWords => new string[] { "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "bool", "break", "case", "catch", "char", "char16_t", "char32_t", "class", "compl", "const", "constexpr", "const_cast", "continue", "decltype", "default", "delete", "do", "double", "dynamic_cast", "else", "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto", "if", "inline", "int", "long", "mutable", "namespace", "new", "noexcept", "not", "not_eq", "nullptr", "operator", "or", "or_eq", "private", "protected", "public", "register", "reinterpret_cast", "return", "short", "signed", "sizeof", "static", "static_assert", "static_cast", "struct", "switch", "template", "this", "thread_local", "throw", "true", "try", "typedef", "typeid", "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq", "override", "final" };
        public string[] Operators => new string[] { "(", ")", "[", "]", "{", "}", "<", ">", "::", ".", "->", "++", "--", "...", ".*", "->", ";", "=", "?", ":", "#", ">=", "<=", "==", "!=", "+", "-", "*", "/", "%", "+=", "-=", "*=", "/=", "%=", "<<", "<<=", ">>", ">>=", "~", "~=", "^", "^=", "|", "|=", "&", "&=", "&&", "||", ",", "->*" };
        public ICollection<int> Tokens { get; } = new List<int>();

        public void Parse(ILexerFactory lexer, string code)
        {
            // base initialize
            AntlrInputStream inputStream = new AntlrInputStream(code);
            var cppLexer = lexer.Create(inputStream);

            var tokens = cppLexer.GetAllTokens();

            foreach (var token in tokens)
            {
                // trying to get type of the token
                // keyword?
                if (KeyWords.Contains(token.Text))
                    Tokens.Add(LexerConstants.KeyWordsStart + Array.IndexOf(KeyWords, token.Text));
                // operator?
                else if (Operators.Contains(token.Text))
                    Tokens.Add(LexerConstants.OpStart + Array.IndexOf(Operators, token.Text));
                // string/char?
                else if (token.Text.Last() == '\"' || token.Text.Last() == '\'')
                    Tokens.Add(LexerConstants.String);
                else if (char.IsDigit(token.Text.First()))
                {
                    // int?
                    if (token.Text.Contains('.'))
                        Tokens.Add(LexerConstants.Real);
                    // real?
                    else
                        Tokens.Add(LexerConstants.Number);
                }
                // id?
                else if (char.IsLetter(token.Text.First()))
                {
                    Tokens.Add(LexerConstants.Id);
                }
                // ?????
                else
                {
                    Tokens.Add(LexerConstants.Unknown);
                }
            }
            
        }
    }
}