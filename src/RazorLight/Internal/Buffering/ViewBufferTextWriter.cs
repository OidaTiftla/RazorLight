﻿using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace RazorLight.Internal.Buffering
{
	/// <summary>
	/// <para>
	/// A <see cref="TextWriter"/> that is backed by a unbuffered writer (over the Response stream) and/or a 
	/// <see cref="ViewBuffer"/>
	/// </para>
	/// <para>
	/// When <c>Flush</c> or <c>FlushAsync</c> is invoked, the writer copies all content from the buffer to
	/// the writer and switches to writing to the unbuffered writer for all further write operations.
	/// </para>
	/// </summary>
	public class ViewBufferTextWriter : TextWriter
	{
		private readonly TextWriter _inner;

		/// <summary>
		/// Creates a new instance of <see cref="ViewBufferTextWriter"/>.
		/// </summary>
		/// <param name="buffer">The <see cref="ViewBuffer"/> for buffered output.</param>
		/// <param name="encoding">The <see cref="System.Text.Encoding"/>.</param>
		public ViewBufferTextWriter(ViewBuffer buffer, Encoding encoding)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (encoding == null)
			{
				throw new ArgumentNullException(nameof(encoding));
			}

			Buffer = buffer;
			Encoding = encoding;
		}

		/// <summary>
		/// Creates a new instance of <see cref="ViewBufferTextWriter"/>.
		/// </summary>
		/// <param name="buffer">The <see cref="ViewBuffer"/> for buffered output.</param>
		/// <param name="encoding">The <see cref="System.Text.Encoding"/>.</param>
		/// <param name="inner">
		/// The inner <see cref="TextWriter"/> to write output to when this instance is no longer buffering.
		/// </param>
		public ViewBufferTextWriter(ViewBuffer buffer, Encoding encoding, TextWriter inner)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (encoding == null)
			{
				throw new ArgumentNullException(nameof(encoding));
			}

			if (inner == null)
			{
				throw new ArgumentNullException(nameof(inner));
			}

			Buffer = buffer;
			Encoding = encoding;
			_inner = inner;
		}

		/// <inheritdoc />
		public override Encoding Encoding { get; }

		/// <inheritdoc />
		public bool IsBuffering { get; private set; } = true;

		/// <summary>
		/// Gets the <see cref="ViewBuffer"/>.
		/// </summary>
		public ViewBuffer Buffer { get; }

		/// <inheritdoc />
		public override void Write(char value)
		{
			if (IsBuffering)
			{
				Buffer.Append(value.ToString());
			}
			else
			{
				_inner.Write(value);
			}
		}

		/// <inheritdoc />
		public override void Write(char[] buffer, int index, int count)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (index < 0 || index >= buffer.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			if (count < 0 || (buffer.Length - index < count))
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			if (IsBuffering)
			{
				Buffer.Append(new string(buffer, index, count));
			}
			else
			{
				_inner.Write(buffer, index, count);
			}
		}

		/// <inheritdoc />
		public override void Write(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			if (IsBuffering)
			{
				Buffer.Append(value);
			}
			else
			{
				_inner.Write(value);
			}
		}

		/// <inheritdoc />
		public override void Write(object value)
		{
			if (value == null)
			{
				return;
			}

			IStringContentContainer container;
			string content;
			if ((container = value as IStringContentContainer) != null)
			{
				Write(container);
			}
			else if ((content = value as string) != null)
			{
				Write(content);
			}
			else
			{
				Write(value.ToString());
			}
		}

		/// <summary>
		/// Writes an <see cref="IStringContentContainer"/> value.
		/// </summary>
		/// <param name="value">The <see cref="IStringContentContainer"/> value.</param>
		public void Write(IStringContentContainer value)
		{
			if (value == null)
			{
				return;
			}

			if (IsBuffering)
			{
				value.MoveTo(Buffer);
			}
			else
			{
				value.WriteTo(_inner);
			}
		}

		/// <inheritdoc />
		public override void WriteLine(object value)
		{
			if (value == null)
			{
				return;
			}

			IStringContentContainer container;
			string content;
			if ((container = value as IStringContentContainer) != null)
			{
				Write(container);
				Write(NewLine);
			}
			else if ((content = value as string) != null)
			{
				Write(content);
				Write(NewLine);
			}
			else
			{
				Write(value.ToString());
				Write(NewLine);
			}
		}

		/// <inheritdoc />
		public override Task WriteAsync(char value)
		{
			if (IsBuffering)
			{
				Buffer.Append(value.ToString());
				return Task.CompletedTask;
			}
			else
			{
				return _inner.WriteAsync(value);
			}
		}

		/// <inheritdoc />
		public override Task WriteAsync(char[] buffer, int index, int count)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}
			if (count < 0 || (buffer.Length - index < count))
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			if (IsBuffering)
			{
				Buffer.Append(new string(buffer, index, count));
				return Task.CompletedTask;
			}
			else
			{
				return _inner.WriteAsync(buffer, index, count);
			}
		}

		/// <inheritdoc />
		public override Task WriteAsync(string value)
		{
			if (IsBuffering)
			{
				Buffer.Append(value);
				return Task.CompletedTask;
			}
			else
			{
				return _inner.WriteAsync(value);
			}
		}

		/// <inheritdoc />
		public override void WriteLine()
		{
			if (IsBuffering)
			{
				Buffer.Append(NewLine);
			}
			else
			{
				_inner.WriteLine();
			}
		}

		/// <inheritdoc />
		public override void WriteLine(string value)
		{
			if (IsBuffering)
			{
				Buffer.Append(value);
				Buffer.Append(NewLine);
			}
			else
			{
				_inner.WriteLine(value);
			}
		}

		/// <inheritdoc />
		public override Task WriteLineAsync(char value)
		{
			if (IsBuffering)
			{
				Buffer.Append(value.ToString());
				Buffer.Append(NewLine);
				return Task.CompletedTask;
			}
			else
			{
				return _inner.WriteLineAsync(value);
			}
		}

		/// <inheritdoc />
		public override Task WriteLineAsync(char[] value, int start, int offset)
		{
			if (IsBuffering)
			{
				Buffer.Append(new string(value, start, offset));
				Buffer.Append(NewLine);
				return Task.CompletedTask;
			}
			else
			{
				return _inner.WriteLineAsync(value, start, offset);
			}
		}

		/// <inheritdoc />
		public override Task WriteLineAsync(string value)
		{
			if (IsBuffering)
			{
				Buffer.Append(value);
				Buffer.Append(NewLine);
				return Task.CompletedTask;
			}
			else
			{
				return _inner.WriteLineAsync(value);
			}
		}

		/// <inheritdoc />
		public override Task WriteLineAsync()
		{
			if (IsBuffering)
			{
				Buffer.Append(NewLine);
				return Task.CompletedTask;
			}
			else
			{
				return _inner.WriteLineAsync();
			}
		}

		/// <summary>
		/// Copies the buffered content to the unbuffered writer and invokes flush on it.
		/// Additionally causes this instance to no longer buffer and direct all write operations
		/// to the unbuffered writer.
		/// </summary>
		public override void Flush()
		{
			if (_inner == null || _inner is ViewBufferTextWriter)
			{
				return;
			}

			if (IsBuffering)
			{
				IsBuffering = false;
				Buffer.WriteTo(_inner);
				Buffer.Clear();
			}

			_inner.Flush();
		}

		/// <summary>
		/// Copies the buffered content to the unbuffered writer and invokes flush on it.
		/// Additionally causes this instance to no longer buffer and direct all write operations
		/// to the unbuffered writer.
		/// </summary>
		/// <returns>A <see cref="Task"/> that represents the asynchronous copy and flush operations.</returns>
		public override async Task FlushAsync()
		{
			if (_inner == null || _inner is ViewBufferTextWriter)
			{
				return;
			}

			if (IsBuffering)
			{
				IsBuffering = false;
				await Buffer.WriteToAsync(_inner);
				Buffer.Clear();
			}

			await _inner.FlushAsync();
		}
	}
}
