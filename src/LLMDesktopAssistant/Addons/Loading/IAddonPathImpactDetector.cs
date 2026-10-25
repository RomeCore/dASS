using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Addons.Loading
{
	public interface IAddonPathImpactDetector
	{
		/// <summary>
		/// Detects impacted addon kinds that should be invalidated when specified path is touched.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The detection is based on the potential addon locations (effective working directories, addon search
		/// folders and explicitly configured packs) and the addon type folder layouts. The detector never accesses
		/// the filesystem: the caller describes what happens with the path (<paramref name="operation"/> and,
		/// when needed, <paramref name="existingAncestorDirectory"/>), so the detection is valid for paths that
		/// do not exist yet. The result is used both for the addon invalidation (<c>IAddonManagerInvalidator</c>)
		/// and for the tool behaviour policy (see <c>AddonKindToolBehaviourConverter</c>).
		/// </para>
		/// <para>
		/// The result is a combination of the impacted kinds, for example <c>Skill | Pack</c> when a file is being
		/// written into 'packs/my-pack/skills/my-skill/SKILL.md' and the pack directory is created together with it.
		/// Touching a pack root, the 'packs' container or a whole pack directory reports <c>AddonKind.All</c>,
		/// because the pack with addons of every kind inside it can appear or disappear entirely.
		/// </para>
		/// </remarks>
		/// <param name="path">
		/// The touched path; it can be a file path or a directory path. Relative paths are resolved against the
		/// current effective working directory of the chat, absolute paths are used as is.
		/// </param>
		/// <param name="isFile">
		/// Whether the provided path is a file (<see langword="true"/>), a directory (<see langword="false"/>),
		/// or unknown (<see langword="null"/>, then both interpretations are applied).
		/// </param>
		/// <param name="operation">
		/// What happens with the path: <see cref="FileOperation.Create"/>, <see cref="FileOperation.Edit"/> or
		/// <see cref="FileOperation.Delete"/>. Determines whether the directories on the path can appear
		/// (create), disappear (delete) or only the content of an existing path changes (edit).
		/// </param>
		/// <param name="existingAncestorDirectory">
		/// The existing directory the operation starts from: every directory strictly below it is considered
		/// as created by the operation (used by <see cref="FileOperation.Create"/> only). Must be an actual
		/// ancestor of the path - the detector trusts the caller and never checks the filesystem. Pass
		/// <see langword="null"/> when unknown; then the worst case is assumed (every directory of the path
		/// may be missing, so all the pack structures on the path are treated as potentially created).
		/// </param>
		/// <returns>
		/// The combination of the impacted addon kinds, or <see cref="AddonKind.None"/> when the path cannot
		/// impact any addon.
		/// </returns>
		AddonKind Detect(string path, bool? isFile,
			FileOperation operation = FileOperation.Create,
			string? existingAncestorDirectory = null);
	}
}
