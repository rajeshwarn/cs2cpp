﻿////#define EMPTY_SKELETON
namespace Il2Native.Logic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using DOM;
    using DOM2;

    using Il2Native.Logic.DOM.Implementations;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Symbols;

    public abstract class CCodeWriterBase
    {
        private static readonly ObjectIDGenerator ObjectIdGenerator = new ObjectIDGenerator();

        private static readonly IDictionary<string, long> StringIdGenerator = new SortedDictionary<string, long>();

        public static long GetId(object obj, out bool firstTime)
        {
            lock (ObjectIdGenerator)
            {
                return ObjectIdGenerator.GetId(obj, out firstTime);
            }
        }

        public static long GetId(string obj)
        {
            lock (StringIdGenerator)
            {
                long id;
                if (StringIdGenerator.TryGetValue(obj, out id))
                {
                    return id;
                }

                id = StringIdGenerator.Count + 1;
                StringIdGenerator[obj] = id;

                return id;
            }
        }

        public abstract void OpenBlock();

        public abstract void EndBlock();

        public abstract void EndBlockWithoutNewLine();

        public abstract void EndStatement();

        public abstract void TextSpan(string line);

        public abstract void TextSpanNewLine(string line);

        public abstract void WhiteSpace();

        public abstract void NewLine();

        public abstract void Separate();

        public abstract void IncrementIndent();

        public abstract void DecrementIndent();

        public abstract void SaveAndSet0Indent();

        public abstract void RestoreIndent();

        public abstract void RequireEmptyStatement();

        internal void WriteMethodBody(BoundStatement boundBody, IMethodSymbol methodSymbol)
        {
#if EMPTY_SKELETON
            this.NewLine();
            this.OpenBlock();
            this.TextSpanNewLine("throw 0xC000C000;");
            this.EndBlock();
#else
            if (boundBody != null)
            {
                var methodBase = Base.Deserialize(boundBody, true);
                methodBase.WriteTo(this);
            }
            else
            {
                this.NewLine();
                this.OpenBlock();
                this.EndBlock();
            }
#endif
        }

        public void WriteNamespace(INamespaceSymbol namespaceSymbol)
        {
            var any = false;
            foreach (var namespaceNode in namespaceSymbol.EnumNamespaces())
            {
                if (any)
                {
                    TextSpan("::");
                }

                any = true;

                WriteNamespaceName(namespaceNode);
            }
        }

        public void WriteNamespaceName(INamespaceSymbol namespaceNode)
        {
            if (namespaceNode.IsGlobalNamespace)
            {
                TextSpan(namespaceNode.ContainingAssembly.MetadataName.CleanUpName());
            }
            else
            {
                TextSpan(namespaceNode.MetadataName);
            }
        }

        public void WriteName(ISymbol symbol)
        {
            TextSpan((symbol.MetadataName ?? symbol.Name).CleanUpName());
        }

        public void WriteNameEnsureCompatible(ISymbol symbol)
        {
            TextSpan((symbol.MetadataName ?? symbol.Name).CleanUpName().EnsureCompatible());
        }

        public void WriteUniqueNameByContainingType(ISymbol symbol)
        {
            var name = symbol.MetadataName ?? symbol.Name;
            var uniqueName = string.Concat(name, GetId(((TypeSymbol)symbol.ContainingType).ToKeyString()));
            TextSpan(uniqueName.CleanUpName());
        }

        public void WriteMethodName(IMethodSymbol methodSymbol, bool allowKeywords = true, bool addTemplate = false, IMethodSymbol methodSymbolForName = null)
        {
            if (addTemplate && methodSymbol.IsGenericMethod)
            {
                this.TextSpan("template");
                this.WhiteSpace();
            }

            if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface)
            {
                TextSpan(methodSymbol.ContainingType.GetTypeFullName());
                TextSpan("_");
            }

            // name
            if (methodSymbol.MethodKind == MethodKind.Constructor)
            {
                WriteTypeName((INamedTypeSymbol)methodSymbol.ReceiverType, false);
                return;
            }
            else
            {
                WriteName(methodSymbolForName ?? methodSymbol);
                if (methodSymbol.MetadataName == "op_Explicit")
                {
                    TextSpan("_");
                    WriteTypeSuffix(methodSymbol.ReturnType);
                }
                else if (methodSymbol.IsStatic && methodSymbol.MetadataName == "op_Implicit")
                {
                    TextSpan("_");
                    WriteTypeSuffix(methodSymbol.ReturnType);
                }
            }

            // write suffixes for ref & out parameters
            foreach (var parameter in methodSymbol.Parameters.Where(p => p.RefKind != RefKind.None))
            {
                TextSpan("_");
                TextSpan(parameter.RefKind.ToString());
            }

            if (methodSymbol.IsGenericMethod)
            {
                if (methodSymbol.IsAbstract || methodSymbol.IsVirtual || methodSymbol.IsOverride)
                {
                    TextSpan("T");
                    this.TextSpan(methodSymbol.Arity.ToString());
                }
                else if (addTemplate)
                {
                    TextSpan("<");

                    var anyTypeArg = false;
                    foreach (var typeArg in methodSymbol.TypeArguments)
                    {
                        if (anyTypeArg)
                        {
                            TextSpan(", ");
                        }

                        anyTypeArg = true;
                        this.WriteType(typeArg);
                    }

                    TextSpan(">");
                }
            }
        }

        public void WriteTypeSuffix(ITypeSymbol type)
        {
            if (WriteSpecialType(type))
            {
                return;
            }

            switch (type.TypeKind)
            {
                case TypeKind.ArrayType:
                    var elementType = ((ArrayTypeSymbol)type).ElementType;
                    WriteTypeSuffix(elementType);
                    TextSpan("Array");
                    return;
                case TypeKind.PointerType:
                    var pointedAtType = ((PointerTypeSymbol)type).PointedAtType;
                    WriteTypeSuffix(pointedAtType);
                    TextSpan("Ptr");
                    return;
                case TypeKind.TypeParameter:
                    WriteName(type);
                    return;
                default:
                    WriteTypeName((INamedTypeSymbol)type);
                    break;
            }
        }

        public void WriteTypeFullName(ITypeSymbol type, bool allowKeywords = true)
        {
            if (type.TypeKind == TypeKind.TypeParameter)
            {
                WriteName(type);
                return;
            }

            var namedType = type as INamedTypeSymbol;
            if (namedType != null)
            {
                WriteTypeFullName(namedType, allowKeywords);
            }
        }

        public void WriteTypeFullName(INamedTypeSymbol type, bool allowKeywords = true, bool valueName = false)
        {
            if (allowKeywords && (type.SpecialType == SpecialType.System_Object || type.SpecialType == SpecialType.System_String))
            {
                WriteTypeName(type, allowKeywords);
                return;
            }

            if (type.ContainingNamespace != null)
            {
                WriteNamespace(type.ContainingNamespace);
                TextSpan("::");
            }

            WriteTypeName(type, allowKeywords, valueName);

            if (type.IsGenericType)
            {
                WriteTemplateDefinition(type);
            }
        }

        public void WriteTypeName(INamedTypeSymbol type, bool allowKeywords = true, bool valueName = false)
        {
            if (allowKeywords)
            {
                if (type.SpecialType == SpecialType.System_Object)
                {
                    TextSpan("object");
                    return;
                }

                if (type.SpecialType == SpecialType.System_String)
                {
                    TextSpan("string");
                    return;
                }
            }

            if (valueName && type.TypeKind == TypeKind.Enum)
            {
                TextSpan("enum_");
            }

            if (type.ContainingType != null)
            {
                WriteTypeName(type.ContainingType);
                TextSpan("_");
            }

            WriteName(type);
        }

        public void WriteType(ITypeSymbol type, bool suppressReference = false, bool allowKeywords = true, bool valueTypeAsClass = false)
        {
            if (!valueTypeAsClass && WriteSpecialType(type))
            {
                return;
            }

            switch (type.TypeKind)
            {
                case TypeKind.Unknown:
                    break;
                case TypeKind.ArrayType:
                    WriteCArrayTemplate((IArrayTypeSymbol)type, !suppressReference, true, allowKeywords);
                    return;
                case TypeKind.Delegate:
                case TypeKind.Interface:
                case TypeKind.Class:
                    WriteTypeFullName(type, allowKeywords);
                    if (type.IsReferenceType && !suppressReference)
                    {
                        TextSpan("*");
                    }

                    return;
                case TypeKind.DynamicType:
                    break;
                case TypeKind.Enum:
                    if (!valueTypeAsClass)
                    {
                        WriteTypeFullName((INamedTypeSymbol)type, allowKeywords, valueName: true);
                    }
                    else
                    {
                        WriteTypeFullName((INamedTypeSymbol)type, allowKeywords);
                        if (!suppressReference && valueTypeAsClass)
                        {
                            TextSpan("*");
                        }
                    }

                    return;
                case TypeKind.Error:
                    // Comment: Unbound Generic in typeof
                    TextSpan("__unbound_generic_type<void>");
                    //WriteName(type);
                    //TextSpan(">");
                    return;
                case TypeKind.Module:
                    break;
                case TypeKind.PointerType:
                    var pointedAtType = ((PointerTypeSymbol)type).PointedAtType;
                    this.WriteType(pointedAtType, allowKeywords: allowKeywords);
                    TextSpan("*");
                    return;
                case TypeKind.Struct:
                    WriteTypeFullName((INamedTypeSymbol)type);
                    if (valueTypeAsClass && !suppressReference)
                    {
                        TextSpan("*");
                    }

                    return;
                case TypeKind.TypeParameter:

                    if (type.ContainingType != null && type.ContainingType.ContainingType != null)
                    {
                        this.WriteUniqueNameByContainingType(type);
                    }
                    else
                    {
                        this.WriteName(type);
                    }

                    return;
                case TypeKind.Submission:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            throw new NotImplementedException();
        }

        public bool WriteSpecialType(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Void:
                    TextSpan("void");
                    return true;
                case SpecialType.System_Boolean:
                    TextSpan("bool");
                    return true;
                case SpecialType.System_Char:
                    TextSpan("wchar_t");
                    return true;
                case SpecialType.System_SByte:
                    TextSpan("int8_t");
                    return true;
                case SpecialType.System_Byte:
                    TextSpan("uint8_t");
                    return true;
                case SpecialType.System_Int16:
                    TextSpan("int16_t");
                    return true;
                case SpecialType.System_UInt16:
                    TextSpan("uint16_t");
                    return true;
                case SpecialType.System_Int32:
                    TextSpan("int32_t");
                    return true;
                case SpecialType.System_UInt32:
                    TextSpan("uint32_t");
                    return true;
                case SpecialType.System_Int64:
                    TextSpan("int64_t");
                    return true;
                case SpecialType.System_UInt64:
                    TextSpan("uint64_t");
                    return true;
                case SpecialType.System_Single:
                    TextSpan("float");
                    return true;
                case SpecialType.System_Double:
                    TextSpan("double");
                    return true;
                case SpecialType.System_Object:
                    if (type.TypeKind == TypeKind.Unknown)
                    {
                        TextSpan("object*");
                        return true;
                    }

                    break;
                case SpecialType.System_String:
                    {
                        TextSpan("string*");
                        return true;
                    }

                    break;
            }

            return false;
        }

        public void WriteFieldDeclaration(IFieldSymbol fieldSymbol)
        {
            if (fieldSymbol.IsStatic)
            {
                TextSpan("static");
                WhiteSpace();
            }

            this.WriteType(fieldSymbol.Type);
            WhiteSpace();
            WriteName(fieldSymbol);
        }

        public void WriteFieldDefinition(IFieldSymbol fieldSymbol)
        {
            if (fieldSymbol.ContainingType.IsGenericType)
            {
                WriteTemplateDeclaration(fieldSymbol.ContainingType);
                NewLine();
            }

            this.WriteType(fieldSymbol.Type);
            WhiteSpace();

            if (fieldSymbol.ContainingNamespace != null)
            {
                WriteNamespace(fieldSymbol.ContainingNamespace);
                TextSpan("::");
            }

            var receiverType = fieldSymbol.ContainingType;
            WriteTypeName(receiverType, false);
            if (receiverType.IsGenericType)
            {
                WriteTemplateDefinition(fieldSymbol.ContainingType);
            }

            TextSpan("::");

            WriteName(fieldSymbol);
        }

        public void WriteMethodDeclaration(IMethodSymbol methodSymbol, bool declarationWithingClass, bool hasBody = false)
        {
            this.WriteMethodPrefixesAndName(methodSymbol, declarationWithingClass);
            this.WriteMethodPatameters(methodSymbol, declarationWithingClass, hasBody);
            this.WriteMethodSuffixes(methodSymbol, declarationWithingClass);
        }

        public void WriteMethodSuffixes(IMethodSymbol methodSymbol, bool declarationWithingClass)
        {
            if (declarationWithingClass)
            {
                if (methodSymbol.IsOverride)
                {
                    this.TextSpan(" override");
                }
                else if (methodSymbol.IsAbstract)
                {
                    this.TextSpan(" = 0");
                }
            }
        }

        public void WriteMethodPatameters(IMethodSymbol methodSymbol, bool declarationWithingClass, bool hasBody)
        {
            // parameters
            var anyParameter = false;
            var notUniqueParametersNames = !declarationWithingClass && methodSymbol.Parameters.Select(p => p.Name).Distinct().Count() != methodSymbol.Parameters.Length;
            var parameterIndex = 0;

            this.TextSpan("(");
            foreach (var parameterSymbol in methodSymbol.Parameters)
            {
                if (anyParameter)
                {
                    this.TextSpan(", ");
                }

                anyParameter = true;

                if (methodSymbol.IsVirtualGenericMethod() && parameterSymbol.Type.TypeKind == TypeKind.TypeParameter)
                {
                    this.WriteType(new TypeImpl { SpecialType = SpecialType.System_Object }, allowKeywords: !declarationWithingClass);
                }
                else
                {
                    this.WriteType(parameterSymbol.Type, allowKeywords: !declarationWithingClass);
                }

                if (parameterSymbol.RefKind != RefKind.None)
                {
                    TextSpan("&");
                }

                if (!declarationWithingClass || hasBody)
                {
                    this.WhiteSpace();
                    if (!notUniqueParametersNames)
                    {
                        this.WriteNameEnsureCompatible(parameterSymbol);
                    }
                    else
                    {
                        this.TextSpan(string.Format("__arg{0}", parameterIndex));
                    }
                }

                parameterIndex++;
            }

            if (anyParameter && methodSymbol.IsVararg)
            {
                this.TextSpan("...");
            }

            this.TextSpan(")");
        }

        public void WriteMethodPrefixesAndName(IMethodSymbol methodSymbol, bool declarationWithingClass)
        {
            if (!declarationWithingClass && methodSymbol.ContainingType.IsGenericType)
            {
                this.WriteTemplateDeclaration(methodSymbol.ContainingType);
                if (!declarationWithingClass)
                {
                    this.NewLine();
                }
            }

            if (methodSymbol.IsGenericMethod && !methodSymbol.IsVirtualGenericMethod())
            {
                this.WriteTemplateDeclaration(methodSymbol);
                if (!declarationWithingClass)
                {
                    this.NewLine();
                }
            }

            if (declarationWithingClass)
            {
                if (methodSymbol.IsStatic)
                {
                    this.TextSpan("static ");
                }

                if (methodSymbol.IsVirtual || methodSymbol.IsOverride || methodSymbol.IsAbstract)
                {
                    this.TextSpan("virtual ");
                }
            }

            this.WriteMethodReturn(methodSymbol, declarationWithingClass);

            if (!declarationWithingClass)
            {
                this.WriteMethodFullName(methodSymbol);
            }
            else
            {
                this.WriteMethodName(methodSymbol, allowKeywords: !declarationWithingClass);
            }
        }

        public void WriteMethodReturn(IMethodSymbol methodSymbol, bool declarationWithingClass)
        {
            // type
            if (methodSymbol.MethodKind != MethodKind.Constructor)
            {
                if (methodSymbol.ReturnsVoid)
                {
                    this.TextSpan("void");
                }
                else if (methodSymbol.IsVirtualGenericMethod() && methodSymbol.ReturnType.TypeKind == TypeKind.TypeParameter)
                {
                    this.WriteType(new TypeImpl { SpecialType = SpecialType.System_Object }, allowKeywords: !declarationWithingClass);
                }
                else
                {
                    this.WriteType(methodSymbol.ReturnType, allowKeywords: !declarationWithingClass);
                }

                this.WhiteSpace();
            }
        }

        public void WriteMethodFullName(IMethodSymbol methodSymbol)
        {
            // namespace
            if (methodSymbol.ContainingNamespace != null)
            {
                WriteNamespace(methodSymbol.ContainingNamespace);
                TextSpan("::");
            }

            var receiverType = (INamedTypeSymbol)methodSymbol.ReceiverType;
            WriteTypeName(receiverType, false);
            if (receiverType.IsGenericType)
            {
                WriteTemplateDefinition(methodSymbol.ContainingType);
            }

            TextSpan("::");

            WriteMethodName(methodSymbol, false);
        }

        public void WriteTemplateDeclaration(INamedTypeSymbol namedTypeSymbol)
        {
            TextSpan("template <");

            var anyTypeParam = false;
            foreach (var typeParam in this.EnumerateTemplateParametersRecursive(namedTypeSymbol).Distinct())
            {
                if (anyTypeParam)
                {
                    TextSpan(",");
                    WhiteSpace();
                }

                TextSpan("typename");
                WhiteSpace();
                if (typeParam.ContainingType != null && typeParam.ContainingType.ContainingType != null)
                {
                    this.WriteUniqueNameByContainingType(typeParam);
                }
                else
                {
                    this.WriteName(typeParam);
                }

                anyTypeParam = true;
            }

            TextSpan("> ");
        }

        public IEnumerable<ITypeParameterSymbol> EnumerateTemplateParametersRecursive(INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol.ContainingType != null)
            {
                foreach (var typeParam in this.EnumerateTemplateParametersRecursive(namedTypeSymbol.ContainingType))
                {
                    yield return typeParam;
                }
            }

            foreach (var typeParam in namedTypeSymbol.TypeParameters)
            {
                yield return typeParam;
            }
        }

        public void WriteTemplateDefinition(INamedTypeSymbol typeSymbol)
        {
            TextSpan("<");

            var anyTypeParam = false;
            foreach (var typeParam in this.EnumerateTemplateArgumentsRecusive(typeSymbol))
            {
                if (anyTypeParam)
                {
                    TextSpan(", ");
                }

                this.WriteType(typeParam);

                anyTypeParam = true;
            }

            TextSpan(">");
        }

        public IEnumerable<ITypeSymbol> EnumerateTemplateArgumentsRecusive(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingType != null)
            {
                foreach (var typeParam in this.EnumerateTemplateArgumentsRecusive(typeSymbol.ContainingType))
                {
                    yield return typeParam;
                }
            }

            foreach (var typeParam in typeSymbol.TypeArguments)
            {
                yield return typeParam;
            }
        }

        public void WriteTemplateDeclaration(IMethodSymbol methodSymbol)
        {
            TextSpan("template <");
            var anyTypeParam = false;
            foreach (var typeParam in methodSymbol.TypeParameters)
            {
                if (anyTypeParam)
                {
                    TextSpan(", ");
                }

                anyTypeParam = true;

                TextSpan("typename ");
                if (typeParam.ContainingType != null && typeParam.ContainingType.ContainingType != null)
                {
                    this.WriteUniqueNameByContainingType(typeParam);
                }
                else
                {
                    this.WriteName(typeParam);
                }

            }

            TextSpan("> ");
        }

        public void WriteAccess(Expression expression)
        {
            var effectiveExpression = expression;

            this.WriteExpressionInParenthesesIfNeeded(effectiveExpression);

            if (effectiveExpression.IsReference)
            {
                if (effectiveExpression is BaseReference)
                {
                    TextSpan("::");
                    return;
                }

                TextSpan("->");
                return;
            }

            if (effectiveExpression.Type.TypeKind == TypeKind.Struct || effectiveExpression.Type.TypeKind == TypeKind.Enum)
            {
                TextSpan(".");
                return;
            }

            // default for Templates
            TextSpan("->");
        }

        public void WriteExpressionInParenthesesIfNeeded(Expression expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            var parenthesis = expression is ObjectCreationExpression || expression is ArrayCreation ||
                              expression is DelegateCreationExpression || expression is BinaryOperator ||
                              expression is UnaryOperator || expression is ConditionalOperator ||
                              expression is AssignmentOperator;

            if (parenthesis)
            {
                this.TextSpan("(");
            }

            expression.WriteTo(this);

            if (parenthesis)
            {
                this.TextSpan(")");
            }
        }

        public void WriteCArrayTemplate(IArrayTypeSymbol arrayTypeSymbol, bool reference = true, bool cleanName = false, bool allowKeywords = true)
        {
            var elementType = arrayTypeSymbol.ElementType;

            if (arrayTypeSymbol.Rank <= 1)
            {
                TextSpan("__array<");
                this.WriteType(elementType, allowKeywords: allowKeywords);
                TextSpan(">");
            }
            else
            {
                TextSpan("__multi_array<");
                this.WriteType(elementType, allowKeywords: allowKeywords);
                TextSpan(",");
                WhiteSpace();
                TextSpan(arrayTypeSymbol.Rank.ToString());
                TextSpan(">");
            }

            if (reference)
            {
                TextSpan("*");
            }
        }

        public void WriteBlockOrStatementsAsBlock(Base node, bool noNewLineAtEnd = false)
        {
            var block = node as Block;
            if (block != null)
            {
                block.SuppressNewLineAtEnd = noNewLineAtEnd;
                block.WriteTo(this);
                return;
            }

            this.OpenBlock();
            node.WriteTo(this);

            if (noNewLineAtEnd)
            {
                this.EndBlockWithoutNewLine();
            }
            else
            {
                this.EndBlock();
            }
        }
    }
}
