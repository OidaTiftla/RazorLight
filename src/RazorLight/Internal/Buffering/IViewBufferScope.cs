using System.IO;

namespace RazorLight.Internal.Buffering
{
	/// <summary>
	/// Creates and manages the lifetime of <see cref="T:string[]"/> instances.
	/// </summary>
	public interface IViewBufferScope
	{
		/// <summary>
		/// Gets a <see cref="T:string[]"/>.
		/// </summary>
		/// <param name="pageSize">The minimum size of the segment.</param>
		/// <returns>The <see cref="T:string[]"/>.</returns>
		string[] GetPage(int pageSize);

		/// <summary>
		/// Returns a <see cref="T:string[]"/> that can be reused.
		/// </summary>
		/// <param name="segment">The <see cref="T:string[]"/>.</param>
		void ReturnSegment(string[] segment);

		/// <summary>
		/// Creates a <see cref="PagedBufferedTextWriter"/> that will delegate to the provided
		/// <paramref name="writer"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/>.</param>
		/// <returns>A <see cref="PagedBufferedTextWriter"/>.</returns>
		PagedBufferedTextWriter CreateWriter(TextWriter writer);
	}
}
