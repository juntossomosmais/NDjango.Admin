using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace NDjango.Admin
{
    public class PropertyList<T> : IReadOnlyList<string>
    {
        private readonly List<string> _names;

        public PropertyList(params Expression<Func<T, object>>[] selectors)
        {
            _names = selectors.Select(Validate).ToList();
        }

        /// <summary>
        /// Reads the property path off the expression: <c>x =&gt; x.Name</c> yields <c>"Name"</c>,
        /// and <c>x =&gt; x.User.Name</c> yields <c>"User.Name"</c>.
        /// </summary>
        /// <remarks>
        /// The path is Django's <c>search_fields = ["user__name"]</c>, and it exists for the same
        /// reason: a join table is identified by what it points at and never by a column of its
        /// own. Without it the only searchable thing on such a table is a foreign key, so finding a
        /// row means looking the parent up on another screen and pasting its id back.
        /// <para>
        /// Anything that is not a chain of member accesses rooted at the parameter is still
        /// refused — a method call, an indexer or a captured variable names no column, and failing
        /// here keeps the error next to the declaration that caused it.
        /// </para>
        /// </remarks>
        private static string Validate(Expression<Func<T, object>> expression)
        {
            var member = expression.Body switch
            {
                MemberExpression m => m,
                UnaryExpression { Operand: MemberExpression m } => m,
                _ => throw new ArgumentException(
                    $"Expression must be a property access (e.g. x => x.Name or x => x.User.Name), but got: {expression}")
            };

            var segments = new List<string>();

            for (Expression? current = member; current is MemberExpression step; current = step.Expression) {
                segments.Insert(0, step.Member.Name);

                if (step.Expression is ParameterExpression)
                    return string.Join(".", segments);
            }

            throw new ArgumentException(
                "Property must be reached from the entity by member access "
                + $"(x => x.{member.Member.Name} or x => x.Related.{member.Member.Name}), but got: {expression}");
        }

        public string this[int index] => _names[index];
        public int Count => _names.Count;
        public IEnumerator<string> GetEnumerator() => _names.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public interface IAdminSettings<T> where T : IAdminSettings<T>
    {
        public PropertyList<T> SearchFields => new();
        public object Actions => null;
    }
}
