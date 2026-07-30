using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Kleiner Pergament-Dialog für generische Text- und Fehlermeldungen (implementiert <see cref="IShowText"/>).
/// Ein Rechtsklick/Esc oder der "Verstanden"-Button schließt ihn.
/// </summary>
public partial class TextDialog : DialogBase, IShowText
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelText;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
	}

	public Task ShowDialog(string text)
	{
		_labelText.Text = text;
		return ShowAndAwait();
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
