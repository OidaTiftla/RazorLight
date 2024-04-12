using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RazorLight.Internal.Buffering
{
	/// <summary>
	/// An <see cref="IStringContentBuilder"/> that is backed by a buffer provided by <see cref="IViewBufferScope"/>.
	/// </summary>
	[DebuggerDisplay("{DebuggerToString()}")]
	public class ViewBuffer : IStringContentBuilder
	{
		public static readonly int PartialViewPageSize = 32;
		public static readonly int TagHelperPageSize = 32;
		public static readonly int ViewComponentPageSize = 32;
		public static readonly int ViewPageSize = 256;

		private readonly IViewBufferScope _bufferScope;
		private readonly string _name;
		private readonly int _pageSize;
		private ViewBufferPage _currentPage;         // Limits allocation if the ViewBuffer has only one page (frequent case).
		private List<ViewBufferPage> _multiplePages; // Allocated only if necessary

		/// <summary>
		/// Initializes a new instance of <see cref="ViewBuffer"/>.
		/// </summary>
		/// <param name="bufferScope">The <see cref="IViewBufferScope"/>.</param>
		/// <param name="name">A name to identify this instance.</param>
		/// <param name="pageSize">The size of buffer pages.</param>
		public ViewBuffer(IViewBufferScope bufferScope, string name, int pageSize)
		{
			if (bufferScope == null)
			{
				throw new ArgumentNullException(nameof(bufferScope));
			}

			if (pageSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(pageSize));
			}

			_bufferScope = bufferScope;
			_name = name;
			_pageSize = pageSize;
		}

		/// <summary>
		/// Get the <see cref="ViewBufferPage"/> count.
		/// </summary>
		public int Count
		{
			get
			{
				if (_multiplePages != null)
				{
					return _multiplePages.Count;
				}
				if (_currentPage != null)
				{
					return 1;
				}
				return 0;
			}
		}

		/// <summary>
		/// Gets a <see cref="ViewBufferPage"/>.
		/// </summary>
		public ViewBufferPage this[int index]
		{
			get
			{
				if (_multiplePages != null)
				{
					return _multiplePages[index];
				}
				if (index == 0 && _currentPage != null)
				{
					return _currentPage;
				}
				throw new IndexOutOfRangeException();
			}
		}

		/// <inheritdoc />
		public IStringContentBuilder Append(string content)
		{
			if (content == null)
			{
				return this;
			}

			// Text that needs encoding is the uncommon case in views, which is why it
			// creates a wrapper and pre-encoded text does not.
			AppendValue(content);
			return this;
		}

		private void AppendValue(string value)
		{
			var page = GetCurrentPage();
			page.Append(value);
		}

		private ViewBufferPage GetCurrentPage()
		{
			if (_currentPage == null || _currentPage.IsFull)
			{
				AddPage(new ViewBufferPage(_bufferScope.GetPage(_pageSize)));
			}
			return _currentPage;
		}

		private void AddPage(ViewBufferPage page)
		{
			if (_multiplePages != null)
			{
				_multiplePages.Add(page);
			}
			else if (_currentPage != null)
			{
				_multiplePages = new List<ViewBufferPage>(2);
				_multiplePages.Add(_currentPage);
				_multiplePages.Add(page);
			}

			_currentPage = page;
		}

		/// <inheritdoc />
		public IStringContentBuilder Clear()
		{
			_multiplePages = null;
			_currentPage = null;
			return this;
		}

		/// <inheritdoc />
		public void WriteTo(TextWriter writer)
		{
			if (writer == null)
			{
				throw new ArgumentNullException(nameof(writer));
			}

			for (var i = 0; i < Count; i++)
			{
				var page = this[i];
				for (var j = 0; j < page.Count; j++)
				{
					var value = page.Buffer[j];
					writer.Write(value);
					continue;
				}
			}
		}

		/// <summary>
		/// Writes the buffered content to <paramref name="writer"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/>.</param>
		/// <returns>A <see cref="Task"/> which will complete once content has been written.</returns>
		public async Task WriteToAsync(TextWriter writer)
		{
			if (writer == null)
			{
				throw new ArgumentNullException(nameof(writer));
			}

			for (var i = 0; i < Count; i++)
			{
				var page = this[i];
				for (var j = 0; j < page.Count; j++)
				{
					var value = page.Buffer[j];
					await writer.WriteAsync(value);
					continue;
				}
			}
		}

		private string DebuggerToString() => _name;

		public void CopyTo(IStringContentBuilder destination)
		{
			if (destination == null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			for (var i = 0; i < Count; i++)
			{
				var page = this[i];
				for (var j = 0; j < page.Count; j++)
				{
					var value = page.Buffer[j];
					destination.Append(value);
				}
			}
		}

		public void MoveTo(IStringContentBuilder destination)
		{
			if (destination == null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			// Perf: We have an efficient implementation when the destination is another view buffer,
			// we can just insert our pages as-is.
			if (destination is ViewBuffer other)
			{
				MoveTo(other);
				return;
			}

			for (var i = 0; i < Count; i++)
			{
				var page = this[i];
				for (var j = 0; j < page.Count; j++)
				{
					var value = page.Buffer[j];
					destination.Append(value);
				}
			}

			for (var i = 0; i < Count; i++)
			{
				var page = this[i];
				Array.Clear(page.Buffer, 0, page.Count);
				_bufferScope.ReturnSegment(page.Buffer);
			}

			Clear();
		}

		private void MoveTo(ViewBuffer destination)
		{
			for (var i = 0; i < Count; i++)
			{
				var page = this[i];

				var destinationPage = destination.Count == 0 ? null : destination[destination.Count - 1];

				// If the source page is less or equal to than half full, let's copy it's content to the destination
				// page if possible.
				var isLessThanHalfFull = 2 * page.Count <= page.Capacity;
				if (isLessThanHalfFull &&
					destinationPage != null &&
					destinationPage.Capacity - destinationPage.Count >= page.Count)
				{
					// We have room, let's copy the items.
					Array.Copy(
						sourceArray: page.Buffer,
						sourceIndex: 0,
						destinationArray: destinationPage.Buffer,
						destinationIndex: destinationPage.Count,
						length: page.Count);

					destinationPage.Count += page.Count;

					// Now we can return the source page, and it can be reused in the scope of this request.
					Array.Clear(page.Buffer, 0, page.Count);
					_bufferScope.ReturnSegment(page.Buffer);

				}
				else
				{
					// Otherwise, let's just add the source page to the other buffer.
					destination.AddPage(page);
				}

			}

			Clear();
		}
	}
}
