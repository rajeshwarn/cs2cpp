﻿// Mr Oleksandr Duzhar licenses this file to you under the MIT license.
// If you need the License file, please send an email to duzhar@googlemail.com
// 
namespace Il2Native.Logic.DOM.Synthesized
{
    using System.Collections.Immutable;
    using System.Linq;

    using DOM2;
    using Implementations;
    using Microsoft.CodeAnalysis;

    public class CCodeRuntimeTypeConstructorDeclaration : CCodeMethodDeclaration
    {
        public CCodeRuntimeTypeConstructorDeclaration(INamedTypeSymbol type, bool cppConst)
            : base(new RuntimeTypeConstructorMethod(type, cppConst))
        {
            MethodBodyOpt = new MethodBody(Method)
            {
                Statements =
                {
                    new ExpressionStatement
                    {
                        Expression =
                            new AssignmentOperator
                            {
                                Left =
                                    new FieldAccess
                                    {
                                        ReceiverOpt = new ThisReference { Type = type },
                                        Field = new FieldImpl { Name = "m_handle" }
                                    },
                                Right = 
                                    new ObjectCreationExpression
                                    {
                                        Type = type.GetMembers().OfType<IFieldSymbol>().First(f => f.Name == "m_handle").Type,
                                        Arguments = { new Parameter { ParameterSymbol = new ParameterImpl { Name = "value" } } }
                                    }
                            }
                    }
                }
            };
        }

        public class RuntimeTypeConstructorMethod : MethodImpl
        {
            public RuntimeTypeConstructorMethod(INamedTypeSymbol type, bool cppConst)
            {
                MethodKind = MethodKind.Constructor;
                Name = cppConst ? type.GetTypeName() : "_ctor";
                ReceiverType = type;
                ContainingType = type;
                ReturnsVoid = true;
                ReturnType = !cppConst ? new TypeImpl { SpecialType = SpecialType.System_Void } : null;
                Parameters =
                    ImmutableArray.Create<IParameterSymbol>(
                        new ParameterImpl
                            {
                                Name = "value",
                                Type = new PointerTypeImpl { PointedAtType = new TypeImpl { SpecialType = SpecialType.System_Void } }
                            });
            }
        }
    }
}
