using System;
using System.Linq.Expressions;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>A helper class to retrieve reflection info for non-private methods, fields and properties</summary>
	/// 
	public static class SymbolExtensions
	{
		/// <summary>Given a lambda expression that calls a method, returns the method info</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns>The method in the lambda expression</returns>
		///
		public static MethodInfo GetMethodInfo(Expression<Action> expression) => GetMethodInfo((LambdaExpression)expression);

		/// <summary>Given a lambda expression that calls a method, returns the method info</summary>
		/// <typeparam name="T">The generic type</typeparam>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns>The method in the lambda expression</returns>
		///
		public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression) => GetMethodInfo((LambdaExpression)expression);

		/// <summary>Given a lambda expression that calls a method, returns the method info</summary>
		/// <typeparam name="T">The generic type</typeparam>
		/// <typeparam name="TResult">The generic result type</typeparam>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns>The method in the lambda expression</returns>
		///
		public static MethodInfo GetMethodInfo<T, TResult>(Expression<Func<T, TResult>> expression) => GetMethodInfo((LambdaExpression)expression);

		/// <summary>Given a lambda expression that calls a method, returns the method info</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <returns>The method in the lambda expression</returns>
		///
		public static MethodInfo GetMethodInfo(LambdaExpression expression)
		{
			var outermostExpression = expression.Body as MethodCallExpression;

			if (outermostExpression is null)
			{
				if (expression.Body is UnaryExpression ue && ue.Operand is MethodCallExpression me && me.Object is System.Linq.Expressions.ConstantExpression ce && ce.Value is MethodInfo mi)
					return mi;
				throw new ArgumentException("Invalid Expression. Expression should consist of a Method call only.");
			}

			var method = outermostExpression.Method;
			if (method is null)
				throw new Exception($"Cannot find method for expression {expression}");

			return method;
		}

		/// <summary>Given a lambda expression that accesses a field, returns the field info</summary>
		/// <typeparam name="T">The generic field type</typeparam>
		/// <param name="expression">The lambda expression using the field</param>
		/// <returns>The field in the lambda expression</returns>
		///
		public static FieldInfo GetFieldInfo<T>(Expression<Func<T>> expression) => GetFieldInfo((LambdaExpression)expression);

		/// <summary>Given a lambda expression that accesses a field, returns the field info</summary>
		/// <param name="expression">The lambda expression using the field</param>
		/// <returns>The field in the lambda expression</returns>
		///
		public static FieldInfo GetFieldInfo(LambdaExpression expression)
		{
			if (AccessedMember(expression) is FieldInfo field)
				return field;
			throw new ArgumentException("Invalid Expression. Expression should consist of a field access only.", nameof(expression));
		}

		/// <summary>Given a lambda expression that reads a property, returns the property info</summary>
		/// <typeparam name="T">The generic property type</typeparam>
		/// <param name="expression">The lambda expression reading the property, for example <c>() => default(MyClass).MyProperty</c></param>
		/// <returns>The property in the lambda expression</returns>
		/// <exception cref="ArgumentNullException">The expression is null</exception>
		/// <exception cref="ArgumentException">The expression does not read a property. Indexers are not supported.</exception>
		///
		public static PropertyInfo GetPropertyInfo<T>(Expression<Func<T>> expression) => GetPropertyInfo((LambdaExpression)expression);

		/// <summary>Given a lambda expression that reads a property, returns the property info</summary>
		/// <param name="expression">The lambda expression reading the property</param>
		/// <returns>The property in the lambda expression</returns>
		/// <exception cref="ArgumentNullException">The expression is null</exception>
		/// <exception cref="ArgumentException">The expression does not read a property. Indexers are not supported.</exception>
		///
		public static PropertyInfo GetPropertyInfo(LambdaExpression expression)
		{
			if (AccessedMember(expression) is PropertyInfo property)
				return property;
			throw new ArgumentException("Invalid Expression. Expression should consist of a property access only.", nameof(expression));
		}

		static MemberInfo AccessedMember(LambdaExpression expression)
		{
			if (expression is null)
				throw new ArgumentNullException(nameof(expression));

			var body = expression.Body;
			while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryExpression)
				body = unaryExpression.Operand;
			return (body as MemberExpression)?.Member;
		}
	}
}
