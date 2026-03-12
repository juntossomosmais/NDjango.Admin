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

        private static string Validate(Expression<Func<T, object>> expression)
        {
            var member = expression.Body switch
            {
                MemberExpression m => m,
                UnaryExpression { Operand: MemberExpression m } => m,
                _ => throw new ArgumentException(
                    $"Expression must be a direct property access (e.g. x => x.Name), but got: {expression}")
            };

            if (member.Expression is not ParameterExpression)
                throw new ArgumentException(
                    $"Property must be accessed directly on the entity (x => x.{member.Member.Name}), but got: {expression}");

            return member.Member.Name;
        }

        public string this[int index] => _names[index];
        public int Count => _names.Count;
        public IEnumerator<string> GetEnumerator() => _names.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public interface IAdminSettings<T> where T : IAdminSettings<T>
    {
        PropertyList<T> SearchFields => new();
    }
}
