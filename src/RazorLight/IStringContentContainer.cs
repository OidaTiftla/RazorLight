using System.IO;

namespace RazorLight
{
	public interface IStringContentContainer
	{
		void WriteTo(TextWriter writer);
		void CopyTo(IStringContentBuilder destination);
		void MoveTo(IStringContentBuilder destination);
	}
}
