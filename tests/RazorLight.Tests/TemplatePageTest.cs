using Moq;
using RazorLight.Internal;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using RazorLight.Internal.Buffering;
using RazorLight.Tests.Utils;
using Xunit;

namespace RazorLight.Tests
{
	public class TemplatePageTest
	{
		private readonly RenderAsyncDelegate _nullRenderAsyncDelegate = () => Task.FromResult(0);
		private readonly Func<TextWriter, Task> NullAsyncWrite = writer => writer.WriteAsync(string.Empty);

		[Fact]
		public async Task DefineSection_ThrowsIfSectionIsAlreadyDefined()
		{
			// Arrange
			var viewContext = CreateViewContext();
			var page = CreatePage(v =>
			{
				v.DefineSection("qux", _nullRenderAsyncDelegate);
				v.DefineSection("qux", _nullRenderAsyncDelegate);
			});

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() => page.ExecuteAsync());
		}

		[Fact]
		public async Task RenderSection_RendersSectionFromPreviousPage()
		{
			// Arrange
			var expected = "Hello world";
			var viewContext = CreateViewContext();
			var page = CreatePage(v =>
			{
				v.Write(v.RenderSection("bar"));
			});
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "bar", () => page.Output.WriteAsync(expected) }
			};

			// Act
			await page.ExecuteAsync();

			// Assert
			Assert.Equal(expected, page.RenderedContent);
		}

		[Fact]
		public async Task RenderSection_ThrowsIfPreviousSectionWritersIsNotSet()
		{
			// Arrange
			Exception ex = null;
			var page = CreatePage(v =>
			{
				v.Key = "/Views/TestPath/Test.cshtml";
				ex = Assert.Throws<InvalidOperationException>(() => v.RenderSection("bar"));
			});

			// Act & Assert
			await page.ExecuteAsync();
		}

		[Fact]
		public async Task RenderSection_ThrowsIfRequiredSectionIsNotFound()
		{
			// Arrange
			var context = CreateViewContext(viewPath: "/Views/TestPath/Test.cshtml");
			context.ExecutingPageKey = "/Views/Shared/_Layout.cshtml";
			var page = CreatePage(v =>
			{
				v.RenderSection("bar");
			}, context: context);
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "baz", _nullRenderAsyncDelegate }
			};

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() => page.ExecuteAsync());
		}

		[Fact]
		public async Task IgnoreSection_ThrowsIfSectionIsNotFound()
		{
			// Arrange
			var context = CreateViewContext(viewPath: "/Views/TestPath/Test.cshtml");
			context.ExecutingPageKey = "/Views/Shared/_Layout.cshtml";
			var page = CreatePage(v =>
			{
				v.Key = "/Views/TestPath/Test.cshtml";
				v.IgnoreSection("bar");
			}, context);
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "baz", _nullRenderAsyncDelegate }
			};

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() => page.ExecuteAsync());
		}

		[Fact]
		public void IsSectionDefined_ThrowsIfPreviousSectionWritersIsNotRegistered()
		{
			// Arrange
			var page = CreatePage(v =>
			{
				v.Key = "path-to-file";
			});
			page.ExecuteAsync();

			// Act & Assert
			Assert.Throws<InvalidOperationException>(() => page.IsSectionDefined("foo"));
		}

		[Fact]
		public async Task IsSectionDefined_ReturnsFalseIfSectionNotDefined()
		{
			// Arrange
			bool? actual = null;
			var page = CreatePage(v =>
			{
				actual = v.IsSectionDefined("foo");
				v.RenderSection("baz");
				v.RenderBodyPublic();
			});
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "baz", _nullRenderAsyncDelegate }
			};
			page.BodyContent = "body-content";

			// Act
			await page.ExecuteAsync();

			// Assert
			Assert.False(actual);
		}

		[Fact]
		public async Task IsSectionDefined_ReturnsTrueIfSectionDefined()
		{
			// Arrange
			bool? actual = null;
			var page = CreatePage(v =>
			{
				actual = v.IsSectionDefined("baz");
				v.RenderSection("baz");
				v.RenderBodyPublic();
			});
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "baz", _nullRenderAsyncDelegate }
			};
			page.BodyContent = "body-content";

			// Act
			await page.ExecuteAsync();

			// Assert
			Assert.True(actual);
		}

		[Fact]
		public async Task RenderSection_ThrowsIfSectionIsRenderedMoreThanOnce()
		{
			// Arrange
			var page = CreatePage(v =>
			{
				v.Key = "/Views/TestPath/Test.cshtml";
				v.RenderSection("header");
				v.RenderSection("header");
			});
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "header", _nullRenderAsyncDelegate }
			};

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(page.ExecuteAsync);
		}

		[Fact]
		public async Task RenderSectionAsync_ThrowsIfSectionIsRenderedMoreThanOnce()
		{
			// Arrange
			var page = CreatePage(async v =>
			{
				v.Key = "/Views/TestPath/Test.cshtml";
				await v.RenderSectionAsync("header");
				await v.RenderSectionAsync("header");
			});
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "header", _nullRenderAsyncDelegate }
			};

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(page.ExecuteAsync);
		}

		[Fact]
		public async Task RenderSectionAsync_ThrowsIfNotInvokedFromLayoutPage()
		{
			// Arrange
			var page = CreatePage(async v =>
			{
				v.Key = "/Views/TestPath/Test.cshtml";
				await v.RenderSectionAsync("header");
			});

			// Act & Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(page.ExecuteAsync);
		}

		[Fact]
		public async Task EnsureRenderedBodyOrSections_ThrowsIfRenderBodyIsNotCalledFromPage_AndNoSectionsAreDefined()
		{
			// Arrange
			var path = "page-path";
			var page = CreatePage(v =>
			{
			});
			page.Key = path;
			page.BodyContent = "some content";
			await page.ExecuteAsync();

			// Act & Assert
			Assert.Throws<InvalidOperationException>(() => page.EnsureRenderedBodyOrSections());
		}

		[Fact]
		public async Task EnsureRenderedBodyOrSections_SucceedsIfRenderBodyIsNotCalledFromPage_AndNoSectionsAreDefined_AndBodyIgnored()
		{
			// Arrange
			var path = "page-path";
			var page = CreatePage(v =>
			{
			});
			page.Key = path;
			page.BodyContent = "some content";
			page.IgnoreBody();

			// Act & Assert (does not throw)
			await page.ExecuteAsync();
			page.EnsureRenderedBodyOrSections();
		}

		[Fact]
		public async Task EnsureRenderedBodyOrSections_ThrowsIfDefinedSectionsAreNotRendered()
		{
			// Arrange
			var path = "page-path";
			var sectionName = "sectionA";
			var page = CreatePage(v =>
			{
			});
			page.Key = path;
			page.BodyContent = "some content";
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ sectionName, _nullRenderAsyncDelegate }
			};
			await page.ExecuteAsync();

			// Act & Assert
			Assert.Throws<InvalidOperationException>(() => page.EnsureRenderedBodyOrSections());
		}

		[Fact]
		public async Task EnsureRenderedBodyOrSections_SucceedsIfDefinedSectionsAreNotRendered_AndIgnored()
		{
			// Arrange
			var path = "page-path";
			var sectionName = "sectionA";
			var page = CreatePage(v =>
			{
			});
			page.Key = path;
			page.BodyContent = "some content";
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ sectionName, _nullRenderAsyncDelegate }
			};
			page.IgnoreSection(sectionName);

			// Act & Assert (does not throw)
			await page.ExecuteAsync();
			page.EnsureRenderedBodyOrSections();
		}

		[Fact]
		public async Task ExecuteAsync_RendersSectionsThatAreNotIgnored()
		{
			// Arrange
			var path = "page-path";
			var page = CreatePage(async p =>
			{
				p.IgnoreSection("ignored");
				p.Write(await p.RenderSectionAsync("not-ignored-section"));
			});
			page.Key = path;
			page.BodyContent = "some content";
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ "ignored", _nullRenderAsyncDelegate },
				{ "not-ignored-section", () => page.Output.WriteAsync("not-ignored-section-content") }
			};

			// Act
			await page.ExecuteAsync();

			// Assert
			Assert.Equal("not-ignored-section-content", page.RenderedContent);
		}

		[Fact]
		public async Task EnsureRenderedBodyOrSections_SucceedsIfRenderBodyIsNotCalled_ButAllDefinedSectionsAreRendered()
		{
			// Arrange
			var sectionA = "sectionA";
			var sectionB = "sectionB";
			var page = CreatePage(v =>
			{
				v.RenderSection(sectionA);
				v.RenderSection(sectionB);
			});
			page.BodyContent = "some content";
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{ sectionA, _nullRenderAsyncDelegate },
				{ sectionB, _nullRenderAsyncDelegate },
			};

			// Act & Assert (does not throw)
			await page.ExecuteAsync();
			page.EnsureRenderedBodyOrSections();
		}

		[Fact]
		public async Task ExecuteAsync_RendersSectionsAndBody()
		{
			// Arrange
			var expected = string.Join(Environment.NewLine,
									   "Layout start",
									   "Header section",
									   "Async Header section",
									   "body content",
									   "Async Footer section",
									   "Footer section",
									   "Layout end");
			var page = CreatePage(async v =>
			{
				v.WriteLiteral("Layout start" + Environment.NewLine);
				v.Write(v.RenderSection("header"));
				v.Write(await v.RenderSectionAsync("async-header"));
				v.Write(v.RenderBodyPublic());
				v.Write(await v.RenderSectionAsync("async-footer"));
				v.Write(v.RenderSection("footer"));
				v.WriteLiteral("Layout end");
			});
			page.BodyContent = "body content" + Environment.NewLine;
			page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
			{
				{
					"footer", () => page.Output.WriteLineAsync("Footer section")
				},
				{
					"header", () => page.Output.WriteLineAsync("Header section")
				},
				{
					"async-header", () => page.Output.WriteLineAsync("Async Header section")
				},
				{
					"async-footer", () => page.Output.WriteLineAsync("Async Footer section")
				},
			};

			// Act
			await page.ExecuteAsync();

			// Assert
			var actual = page.RenderedContent;
			Assert.Equal(expected, actual);
		}


		[Fact]
		public async Task FlushAsync_InvokesFlushOnWriter()
		{
			// Arrange
			var writer = new Mock<TextWriter>();
			var context = CreateViewContext(writer.Object);
			var page = CreatePage(async p =>
			{
				await p.FlushAsync();
			}, context);

			// Act
			await page.ExecuteAsync();

			// Assert
			writer.Verify(v => v.FlushAsync(), Times.Once());
		}

		[Fact]
		public async Task FlushAsync_ReturnsEmptystring()
		{
			// Arrange
			string actual = null;
			var writer = new Mock<TextWriter>();
			var context = CreateViewContext(writer.Object);
			var page = CreatePage(async p =>
			{
				actual = await p.FlushAsync();
			}, context);

			// Act
			await page.ExecuteAsync();

			// Assert
			Assert.Same(string.Empty, actual);
		}

		[Fact]
		public void PushWriter_SetsUnderlyingWriter()
		{
			// Arrange
			var page = CreatePage(p => { });
			var writer = new StringWriter();

			// Act
			page.PushWriter(writer);

			// Assert
			Assert.Same(writer, page.PageContext.Writer);
		}

		[Fact]
		public void PopWriter_ResetsUnderlyingWriter()
		{
			// Arrange
			var page = CreatePage(p => { });
			var defaultWriter = new StringWriter();
			page.PageContext.Writer = defaultWriter;

			var writer = new StringWriter();

			// Act 1
			page.PushWriter(writer);

			// Assert 1
			Assert.Same(writer, page.PageContext.Writer);

			// Act 2
			var poppedWriter = page.PopWriter();

			// Assert 2
			Assert.Same(defaultWriter, poppedWriter);
			Assert.Same(defaultWriter, page.PageContext.Writer);
		}

		[Fact]
		public void Write_StringValue_UsesSpecifiedWriter_EncodesValue()
		{
			// Arrange
			var page = CreatePage(p => { });
			var bufferWriter = new StringWriter();

			// Act
			page.PushWriter(bufferWriter);
			page.Write("This should be encoded");
			page.PopWriter();

			// Assert
			Assert.Equal("HtmlEncode[[This should be encoded]]", bufferWriter.ToString());
		}

		[Fact]
		public async Task Write_Withstring_WritesValueWithoutEncoding()
		{
			// Arrange
			var buffer = new ViewBuffer(new TestViewBufferScope(), string.Empty, pageSize: 32);
			var writer = new ViewBufferTextWriter(buffer, Encoding.UTF8);

			var page = CreatePage(p =>
			{
				p.Write("Hello world");
			});
			page.PageContext.Writer = writer;

			// Act
			await page.ExecuteAsync();

			// Assert
			using (var stringWriter = new StringWriter())
			{
				buffer.WriteTo(stringWriter);
				Assert.Equal("Hello world", stringWriter.ToString());
			}
		}

		[Fact]
		public async Task Ensure_Raw_String_Is_Not_Encoded()
		{
			string expected = "\"Hello\"";
			var model = new { Data = "\"Hello\"" };

			using (var writer = new StringWriter())
			{
				var context = new PageContext { Writer = writer };

				var page = CreatePage(v =>
				{
					v.Write(v.Raw(model.Data));
				}, context);

				await page.ExecuteAsync();

				string actual = writer.ToString();

				Assert.Equal(expected, actual);
			}
		}

		[Fact]
		public async Task DisableEncoding_True_GloballyDisablesEncoding()
		{
			string expected = "<tag>I am not encoded $& </tag>";

			using (var writer = new StringWriter())
			{
				var context = new PageContext { Writer = writer };
				var page = CreatePage(v =>
				{
					v.Write(expected);
				}, context);

				await page.ExecuteAsync();
				string actual = writer.ToString();

				Assert.Equal(expected, actual);
			}
		}

		#region helpers

		public static TestableRazorPage CreatePage(
			Action<TestableRazorPage> executeAction,
			PageContext context = null)
		{
			return CreatePage(page =>
			{
				executeAction(page);
				return Task.FromResult(0);
			}, context);
		}

		public static TestableRazorPage CreatePage(
			Func<TestableRazorPage, Task> executeAction,
			PageContext context = null)
		{
			context = context ?? CreateViewContext();
			var view = new Mock<TestableRazorPage> { CallBase = true };
			if (executeAction != null)
			{
				view.Setup(v => v.ExecuteAsync())
					.Returns(() =>
					{
						return executeAction(view.Object);
					});
			}

			view.Object.PageContext = context;
			return view.Object;
		}

		public static PageContext CreateViewContext(
			TextWriter writer = null,
			IViewBufferScope bufferScope = null,
			string viewPath = null)
		{
			bufferScope = bufferScope ?? new TestViewBufferScope();
			var buffer = new ViewBuffer(bufferScope, viewPath ?? "TEST", 32);
			writer = writer ?? new ViewBufferTextWriter(buffer, Encoding.UTF8);

			return new PageContext
			{
				Writer = writer
			};
		}

		public class TestViewBufferScope : IViewBufferScope
		{
			public IList<string[]> CreatedBuffers { get; } = new List<string[]>();

			public IList<string[]> ReturnedBuffers { get; } = new List<string[]>();

			public string[] GetPage(int size)
			{
				var buffer = new string[size];
				CreatedBuffers.Add(buffer);
				return buffer;
			}

			public void ReturnSegment(string[] segment)
			{
				ReturnedBuffers.Add(segment);
			}

			public PagedBufferedTextWriter CreateWriter(TextWriter writer)
			{
				return new PagedBufferedTextWriter(ArrayPool<char>.Shared, writer);
			}
		}

		public abstract class TestableRazorPage : TemplatePage
		{
			public string RenderedContent
			{
				get
				{
					var bufferedWriter = Assert.IsType<ViewBufferTextWriter>(Output);
					using (var stringWriter = new StringWriter())
					{
						bufferedWriter.Buffer.WriteTo(stringWriter);
						return stringWriter.ToString();
					}
				}
			}

			public string RenderBodyPublic()
			{
				return base.RenderBody();
			}
		}

		#endregion
	}
}