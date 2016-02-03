﻿namespace Il2Native.Logic.DOM2
{
    using Microsoft.CodeAnalysis.CSharp;

    public class PointerIndirectionOperator : Expression
    {
        public Expression Operand { get; set; }

        internal void Parse(BoundPointerIndirectionOperator pointerIndirectionOperator)
        {
            base.Parse(pointerIndirectionOperator);

            this.Operand = Deserialize(pointerIndirectionOperator.Operand) as Expression;
        }
 
        internal override void WriteTo(CCodeWriterBase c)
        {
            c.TextSpan("*");
            c.WriteExpressionInParenthesesIfNeeded(this.Operand);
        }
    }
}
