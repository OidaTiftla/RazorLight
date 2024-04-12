namespace RazorLight.Internal.Buffering
{
	public class ViewBufferPage
	{
		public ViewBufferPage(string[] buffer)
		{
			Buffer = buffer;
		}

		public string[] Buffer { get; }

		public int Capacity => Buffer.Length;

		public int Count { get; set; }

		public bool IsFull => Count == Capacity;

		public void Append(string value) => Buffer[Count++] = value;
	}
}
