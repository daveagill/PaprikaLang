using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PaprikaLib
{
	public class ListOps
	{
		public static IList<double> GenerateList(double from, double to, double step)
		{
			IList<double> list = new List<double>();

			if (to >= from)
			{
				for (double i = from; i < to; i += step)
				{
					list.Add(i);
				}
			}
			else
			{
				for (double i = from; i > to; i -= step)
				{
					list.Add(i);
				}
			}

			return list;
		}

		public static string ToStringRepresentation(object o)
		{
			IEnumerable enumerable = o as IEnumerable;
			if (enumerable != null)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append('[');

				bool isFirst = true;
				foreach (var elem in enumerable)
				{
					if (!isFirst)
					{
						sb.Append(", ");
					}
					sb.Append(elem);
					isFirst = false;
				}

				sb.Append(']');
				return sb.ToString();
			}

			return o.ToString();
		}
	}
}
