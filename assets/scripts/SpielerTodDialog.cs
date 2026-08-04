using System.Threading.Tasks;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Todesanzeige (Migration von Ort_Spielertod aus dem WinForms-Client): zeigt vor dem
/// Grabstein-Hintergrund (HintSpielerTod) die Todesursache im oberen Banner sowie auf dem Grabstein die
/// Grabinschrift – Name, Lebensdaten und einen zur Spielweise passenden Grabspruch (Issue #15) – samt
/// Hinweis zur Erbfolge. Ein Rechtsklick (oder Esc) schließt die Anzeige.
/// </summary>
public partial class SpielerTodDialog : DialogBase
{
	[Export]
	public NodePath LabelUrsachePath { get; set; }

	[Export]
	public NodePath LabelGrabPath { get; set; }

	private Label _labelUrsache;
	private Label _labelGrab;

	protected override void OnReady()
	{
		_labelUrsache = GetNode<Label>(LabelUrsachePath);
		_labelGrab = GetNode<Label>(LabelGrabPath);
	}

	/// <summary>Zeigt die Todesanzeige mit Todesursache (Banner) und Grabinschrift (auf dem Grabstein).</summary>
	public Task ShowDialog(string todesursache, string grabinschrift)
	{
		_labelUrsache.Text = todesursache;
		_labelGrab.Text = grabinschrift;
		return ShowAndAwait();
	}
}
