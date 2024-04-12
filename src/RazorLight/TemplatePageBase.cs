using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using RazorLight.Internal;
using System.Buffers;
using RazorLight.Internal.Buffering;
using RazorLight.Text;

namespace RazorLight
{
	public abstract class TemplatePageBase : ITemplatePage
	{
		private readonly Stack<TextWriter> _textWriterStack = new Stack<TextWriter>();
		private StringWriter _valueBuffer;
		private IViewBufferScope _bufferScope;
		private TextWriter _pageWriter;
		//private IUrlHelper _urlHelper;

		public abstract void SetModel(object model);

		/// <inheritdoc />
		public virtual PageContext PageContext { get; set; }

		/// <inheritdoc />
		public string BodyContent { get; set; }

		/// <inheritdoc />
		public bool IsLayoutBeingRendered { get; set; }

		/// <inheritdoc />
		public string Layout { get; set; }

		public virtual dynamic ViewBag
		{
			get
			{
				if (PageContext == null)
				{
					throw new InvalidOperationException();
				}

				return PageContext.ViewBag;
			}
		}

		public Func<string, object, Task> IncludeFunc { get; set; }

		/// <inheritdoc />
		public IDictionary<string, RenderAsyncDelegate> PreviousSectionWriters { get; set; }

		/// <inheritdoc />
		public IDictionary<string, RenderAsyncDelegate> SectionWriters { get; } =
			new Dictionary<string, RenderAsyncDelegate>(StringComparer.OrdinalIgnoreCase);

		/// <inheritdoc />
		public string Key { get; set; }

		/// <summary>
		/// Gets the <see cref="TextWriter"/> that the template is writing output to.
		/// </summary>
		public virtual TextWriter Output
		{
			get
			{
				if (PageContext == null)
				{
					throw new InvalidOperationException();
				}

				return PageContext.Writer;
			}
		}

		private IViewBufferScope BufferScope
		{
			get
			{
				if (_bufferScope == null)
				{
					//TODO: replace with services maybe
					//var services = ViewContext.HttpContext.RequestServices;
					//_bufferScope = services.GetRequiredService<IViewBufferScope>();
					_bufferScope = new MemoryPoolViewBufferScope(ArrayPool<string>.Shared, ArrayPool<char>.Shared);
				}

				return _bufferScope;
			}
		}

		/// <inheritdoc />
		public abstract Task ExecuteAsync();

		/// <summary>
		/// Invokes <see cref="TextWriter.FlushAsync"/> on <see cref="Output"/> and <see cref="m:Stream.FlushAsync"/>
		/// on the response stream, writing out any buffered content to the <see cref="Microsoft.AspNetCore.Http.HttpResponse.Body"/>.
		/// </summary>
		/// <returns>A <see cref="Task{string}"/> that represents the asynchronous flush operation and on
		/// completion returns an empty <see cref="string"/>.</returns>
		/// <remarks>The value returned is a token value that allows FlushAsync to work directly in an HTML
		/// section. However the value does not represent the rendered content.
		/// This method also writes out headers, so any modifications to headers must be done before
		/// <see cref="FlushAsync"/> is called. For example, call <see cref="M:Microsoft.AspNetCore.Mvc.Razor.RazorPageBase.SetAntiforgeryCookieAndHeader"/> to send
		/// antiforgery cookie token and X-Frame-Options header to client before this method flushes headers out.
		/// </remarks>
		public virtual async Task<string> FlushAsync()
		{
			// Calls to Flush are allowed if the page does not specify a Layout or if it is executing a section in the
			// Layout.
			if (!IsLayoutBeingRendered && !string.IsNullOrEmpty(Layout))
			{
				throw new InvalidOperationException();
			}

			await Output.FlushAsync();
			return string.Empty;
		}

		public abstract void BeginContext(int position, int length, bool isLiteral);

		public abstract void EndContext();

		public abstract void EnsureRenderedBodyOrSections();

		/// <summary>
		/// Returns the specified string as a raw string. This will ensure it is not encoded.
		/// </summary>
		/// <param name="rawString">The raw string to write.</param>
		/// <returns>An instance of <see cref="IRawString"/>.</returns>
		public IRawString Raw(string rawString)
		{
			return new RawString(rawString);
		}

		public static string HelperFunction(Func<object, string> body)
		{
			return body(null);
		}

		/*
        public virtual string Href(string contentPath)
        {
            if (contentPath == null)
            {
                throw new ArgumentNullException(nameof(contentPath));
            }

            if (_urlHelper == null)
            {
                var services = ViewContext?.HttpContext.RequestServices;
                var factory = services.GetRequiredService<IUrlHelperFactory>();
                _urlHelper = factory.GetUrlHelper(PageContext);
            }

            return _urlHelper.Content(contentPath);
        }*/

		/// <summary>
		/// Creates a named content section in the page that can be invoked in a Layout page using
		/// <c>RenderSection</c> or <c>RenderSectionAsync</c>
		/// </summary>
		/// <param name="name">The name of the section to create.</param>
		/// <param name="section">The delegate to execute when rendering the section.</param>
		/// <remarks>This is a temporary placeholder method to support ASP.NET Core 2.0.0 editor code generation.</remarks>
		[EditorBrowsable(EditorBrowsableState.Never)]
		protected void DefineSection(string name, Func<object, Task> section)
			=> DefineSection(name, () => section(null /* writer */));

		/// <summary>
		/// Creates a named content section in the page that can be invoked in a Layout page using
		/// <c>RenderSection</c> or <c>RenderSectionAsync</c>
		/// </summary>
		/// <param name="name">The name of the section to create.</param>
		/// <param name="section">The <see cref="RenderAsyncDelegate"/> to execute when rendering the section.</param>
		public virtual void DefineSection(string name, RenderAsyncDelegate section)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (section == null)
			{
				throw new ArgumentNullException(nameof(section));
			}

			if (SectionWriters.ContainsKey(name))
			{
				throw new InvalidOperationException();
			}
			SectionWriters[name] = section;
		}

		#region Write section

		/// <summary>
		/// Writes the specified <paramref name="value"/> with HTML encoding to <see cref="Output"/>.
		/// </summary>
		/// <param name="value">The <see cref="object"/> to write.</param>
		public virtual void Write(object value)
		{
			if (value == null || value == string.Empty)
			{
				return;
			}

			var writer = Output;

			switch (value)
			{
				case IRawString raw:
					raw.WriteTo(writer);
					break;
				case string content:
					var bufferedWriter = writer as ViewBufferTextWriter;
					if (bufferedWriter == null || !bufferedWriter.IsBuffering)
					{
						writer.Write(content);
					}
					else
					{
						if (value is IStringContentContainer StringContentContainer)
						{
							// This is likely another ViewBuffer.
							StringContentContainer.MoveTo(bufferedWriter.Buffer);
						}
						else
						{
							// Perf: This is the common case for string, ViewBufferTextWriter is inefficient
							// for writing character by character.
							_ = bufferedWriter.Buffer.Append(content);
						}
					}
					break;
				default:
					Write(value.ToString());
					break;
			}
		}

		/// <summary>
		/// Writes the specified <paramref name="value"/> with HTML encoding to <see cref="Output"/>.
		/// </summary>
		/// <param name="value">The <see cref="string"/> to write.</param>
		public virtual void Write(string value)
		{
			var writer = Output;
			if (!string.IsNullOrEmpty(value))
			{
				writer.Write(value);
			}
		}

		/// <summary>
		/// Writes the specified <paramref name="value"/> without HTML encoding to <see cref="Output"/>.
		/// </summary>
		/// <param name="value">The <see cref="object"/> to write.</param>
		public virtual void WriteLiteral(object value)
		{
			if (value == null)
			{
				return;
			}

			WriteLiteral(value.ToString());
		}

		/// <summary>
		/// Writes the specified <paramref name="value"/> without HTML encoding to <see cref="Output"/>.
		/// </summary>
		/// <param name="value">The <see cref="string"/> to write.</param>
		public virtual void WriteLiteral(string value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				Output.Write(value);
			}
		}

		// Internal for unit testing.
		protected internal virtual void PushWriter(TextWriter writer)
		{
			if (writer == null)
			{
				throw new ArgumentNullException(nameof(writer));
			}

			_textWriterStack.Push(PageContext.Writer);
			PageContext.Writer = writer;
		}

		// Internal for unit testing.
		protected internal virtual TextWriter PopWriter()
		{
			PageContext.Writer = _textWriterStack.Pop();
			return PageContext.Writer;
		}

		private void WritePositionTaggedLiteral(string value, int position)
		{
			BeginContext(position, value.Length, isLiteral: true);
			WriteLiteral(value);
			EndContext();
		}

		#endregion

		#region Helpers

		private bool IsBoolFalseOrNullValue(string prefix, object value)
		{
			return string.IsNullOrEmpty(prefix) &&
				(value == null ||
				(value is bool && !(bool)value));
		}

		private bool IsBoolTrueWithEmptyPrefixValue(string prefix, object value)
		{
			// If the value is just the bool 'true', use the attribute name as the value.
			return string.IsNullOrEmpty(prefix) &&
				(value is bool && (bool)value);
		}

		#endregion
	}
}
