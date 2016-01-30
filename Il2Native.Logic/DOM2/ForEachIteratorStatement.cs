﻿namespace Il2Native.Logic.DOM2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.CodeAnalysis.CSharp;

    public class ForEachIteratorStatement : Statement
    {
        private readonly IList<Statement> locals = new List<Statement>();

        private Base initialization;
        private TryStatement tryStatement;

        private enum Stages
        {
            Initialization,
            TryBody,
            End
        }

        internal bool Parse(BoundStatementList boundStatementList)
        {
            if (boundStatementList == null)
            {
                throw new ArgumentNullException();
            }

            foreach (var boundBlock in boundStatementList.Statements.OfType<BoundBlock>())
            {
                ParseLocals(boundBlock.Locals, locals);
            }

            var stage = Stages.Initialization;
            var statementList = Unwrap(boundStatementList);

            // in case of multi array if current statmentList contains BoundBlock you should process all statements into Initial stage
            if (statementList.Statements.Last() is BoundBlock &&
                !(statementList.Statements.Take(statementList.Statements.Length - 1).All(s => s is BoundBlock)))
            {
                return false;
            }

            foreach (var boundStatement in IterateBoundStatementsList(statementList))
            {
                BoundTryStatement boundTryStatement = null;
                if (stage == Stages.Initialization)
                {
                    boundTryStatement = boundStatement as BoundTryStatement;
                    if (boundTryStatement != null)
                    {
                        stage = Stages.TryBody;
                    }
                    else
                    {
                        var boundGotoStatement = boundStatement as BoundGotoStatement;
                        if (boundGotoStatement != null)
                        {
                            continue;
                        }
                    }
                }

                switch (stage)
                {
                    case Stages.Initialization:
                        var statement = Deserialize(boundStatement, specialCase: SpecialCases.ForEachBody);
                        MergeOrSet(ref this.initialization, statement);
                        break;
                    case Stages.TryBody:
                        this.tryStatement = new TryStatement();

                        // apply special parsing to block of try
                        var whileStatement = new WhileStatement();
                        whileStatement.Parse(boundTryStatement.TryBlock.Statements.OfType<BoundStatementList>().First());

                        this.tryStatement.TryBlock = whileStatement;
                        if (boundTryStatement.FinallyBlockOpt != null)
                        {
                            this.tryStatement.FinallyBlockOpt = Deserialize(
                                boundTryStatement.FinallyBlockOpt,
                                specialCase: SpecialCases.ForEachBody);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return true;
        }

        internal override void WriteTo(CCodeWriterBase c)
        {
            c.OpenBlock();

            foreach (var statement in this.locals)
            {
                statement.WriteTo(c);
            }

            var block = this.initialization as Block;
            if (block != null)
            {
                var any = false;
                foreach (var initializationItem in block.Statements)
                {
                    if (any)
                    {
                        c.TextSpan(",");
                        c.WhiteSpace();
                    }

                    PrintStatementAsExpression(c, initializationItem);
                    any = true;
                }
            }
            else
            {
                PrintStatementAsExpression(c, this.initialization);
            }

            this.tryStatement.WriteTo(c);

            c.EndBlock();

            // No normal ending of Statement as we do not need extra ;
            c.Separate();
        }
    }
}
