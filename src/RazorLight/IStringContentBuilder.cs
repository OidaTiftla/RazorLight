using System.IO;
using System.Threading.Tasks;

namespace RazorLight
{
	public interface IStringContentBuilder
	{
		IStringContentBuilder Append(string content);
		IStringContentBuilder Clear();
		void WriteTo(TextWriter writer);
		Task WriteToAsync(TextWriter writer);
		void CopyTo(IStringContentBuilder destination);
		void MoveTo(IStringContentBuilder destination);
	}
}
