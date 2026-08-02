using System.Threading.Tasks;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Jahresbuch zu Zugbeginn (Migration von Ort_Buch/HintBuchOffen): zeigt vor dem aufgeschlagenen
/// Buch die Produktionsübersicht auf der linken und die Exporte auf der rechten Seite. Ein Rechtsklick
/// (oder Esc) blättert weiter. Ersetzt für das Jahresbuch den generischen Rundennachrichten-Bildschirm.
/// </summary>
public partial class JahresbuchDialog : DialogBase
{
	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath LabelLinksPath { get; set; }

	[Export]
	public NodePath LabelRechtsPath { get; set; }

	private Label _labelTitel;
	private Label _labelLinks;
	private Label _labelRechts;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelLinks = GetNode<Label>(LabelLinksPath);
		_labelRechts = GetNode<Label>(LabelRechtsPath);
	}

	/// <summary>Zeigt das Jahresbuch: <paramref name="linkeSeite"/> (Produktion) links, <paramref name="rechteSeite"/> (Exporte) rechts.</summary>
	public Task ShowDialog(string titel, string linkeSeite, string rechteSeite)
	{
		_labelTitel.Text = titel;
		_labelLinks.Text = linkeSeite;
		_labelRechts.Text = rechteSeite;
		return ShowAndAwait();
	}
}
