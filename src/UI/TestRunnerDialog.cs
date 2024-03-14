using System.Reflection;

using Eto.Drawing;
using Eto.Forms;

namespace NUnitTestRunner.UI
{

	/// <summary>Offers up a UI for the Test Runner</summary>
	internal class TestRunnerDialog : Dialog<DialogResult>
	{
		class NUnitRunArgs : EventArgs
		{
			public enum TraceLevels
			{
				Off, Error, Warning, Info, Verbose
			}
			public TraceLevels TraceLevel { get; set; }
			public List<IListItem> TestItems { get; set; }
			public NUnitRunArgs(List<IListItem> TestItems, TraceLevels Level)
			{
				this.TestItems = TestItems;
				TraceLevel = Level;
			}
		}

		ListBox TestListBox { get; set; }
		List<IListItem> TestItems { get; set; }

		/// <summary>Creates a new TestRunnerDialog</summary>
		public TestRunnerDialog(Assembly testAssembly, NUnitTestRunnerArgs args)
		{
			Padding = new Padding(5);
			Resizable = true;
			Title = "NUnit Test Runner";
			WindowStyle = WindowStyle.Default;
			MinimumSize = new Size(400, 400);

			TestItems = new List<IListItem>();
			TestListBox = new ListBox();

			// Filter tests in the test list box by their name.
			// TODO: consider doing something a bit smarter to preserve the parents
			// when any of their children are matched by the filter.
			TextBox searchBox = new TextBox() {
				PlaceholderText = "Search..."
			};
			searchBox.TextChanged += (_, _) =>
			{
				TestListBox.Items.Clear();
				if (string.IsNullOrEmpty(searchBox.Text))
				{
					foreach (var item in TestItems)
						TestListBox.Items.Add(item);
				}
				else
				{
					foreach (var item in TestItems)
						if (item.Text.ToLower().Contains(searchBox.Text.ToLower()))
							TestListBox.Items.Add(item);
				}
			};

			var runAllButton = new Button { Text = "RunAll" };
			runAllButton.Click += (_, _) =>
				ExecuteTests(args, new NUnitRunArgs(TestListBox.Items.ToList(), NUnitRunArgs.TraceLevels.Off));

			var runButton = new Button { Text = "Run" };
			runButton.Click += (_, _) =>
			{
				var singleTest = new List<IListItem>() { TestListBox.Items[Math.Max(0, TestListBox.SelectedIndex)] };
				ExecuteTests(args, new NUnitRunArgs(singleTest, NUnitRunArgs.TraceLevels.Off));
			};

			var debugButton = new Button { Text = "Debug" };
			debugButton.Click += (_, _) =>
			{
				var singleTest = new List<IListItem>() { TestListBox.Items[Math.Max(0, TestListBox.SelectedIndex)] };
				ExecuteTests(args, new NUnitRunArgs(singleTest, NUnitRunArgs.TraceLevels.Verbose));
			};

			var closeButton = new Button { Text = "Close" };
			closeButton.Click += (sender, e) => Close(DialogResult.Ok);

			DefaultButton = closeButton;

			var tests = testAssembly.GetExportedTypes()
				.Select(type => type.GetMethods().Where(typ =>
							Attribute.IsDefined(typ, typeof(NUnit.Framework.TestAttribute)) ||
							Attribute.IsDefined(typ, typeof(NUnit.Framework.TestCaseAttribute)) ||
							Attribute.IsDefined(typ, typeof(NUnit.Framework.IgnoreAttribute))
					));

			var items = tests;
			if (items is not null)
			{
				foreach (var item in items)
				{
					var methods = item;
					if (!methods.Any())
						continue;

					var value = item.First().DeclaringType;
					var clsAttr = value.Attributes;
					string typeName = $"{value.Name} ({item.Count()})";
					TestItems.Add(new ListItem() { Text = typeName, Key = value.FullName, Tag = methods });
					foreach (var method in methods)
					{
						string text = string.Empty;
						if (method.GetCustomAttribute<NUnit.Framework.IgnoreAttribute>() is NUnit.Framework.IgnoreAttribute ignoreAttribute)
						{
							text = $"  ├── {method.Name} (Ignored)";
						}
						else
						{
							text = $"  ├── {method.Name}";
						}

						var testItem = new ListItem() { Text = text, Key = method.Name, Tag = method };
						TestItems.Add(testItem);
					}
				}
			}

			TestListBox.Height = 75;
			foreach (var item in TestItems)
				TestListBox.Items.Add(item);


			var defaultLayout = new TableLayout
			{
				Padding = new Padding(5, 10, 5, 5),
				Spacing = new Size(5, 5),
				Rows = { new TableRow(runAllButton, runButton, debugButton, closeButton) }
			};

			Content = new TableLayout
			{
				Padding = new Padding(5),
				Spacing = new Size(5, 5),
				Rows = {
					searchBox,
					new TableRow(defaultLayout),
					TestListBox
				}
			};

		}

		private void ExecuteTests(NUnitTestRunnerArgs args, NUnitRunArgs e)
		{
			Visible = false;

			try
			{
				// TODO: This line is what makes the RunAll button do decidedly nothing.
				// We should either remove the button or loop over the list of items.
				if (e.TestItems.Count == 1)
				{
					string className = null;
					string methodName = null;

					var tests = (e.TestItems.First() as ListItem).Tag;
					if (tests is MethodInfo t)
					{
						methodName = t.Name;
						className = t.DeclaringType.Name;
					}
					else
					{
						Type declaringType = (tests as IEnumerable<MethodInfo>).First().DeclaringType;
						className = declaringType.Name;
					}

					var runner = new RhinoTestRunner(args);
					runner.Run(new SelectedRhinoTestFilter(methodName, className));
				}


			}
			catch (Exception ex)
			{
				RhinoApp.Write(ex.ToString());
			}
			finally
			{
				Visible = true;
			}

		}

	}
}
