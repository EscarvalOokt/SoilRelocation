using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public sealed class DeployModPackage : Task
{
    [Required] public string Root { get; set; }
    [Required] public string RepositoryRoot { get; set; }
    [Required] public string GameRoot { get; set; }
    [Required] public string PackageId { get; set; }
    [Required] public ITaskItem[] Directories { get; set; }
    public bool ValidateOnly { get; set; }
    public ITaskItem[] Files { get; set; }
    public string CurrentAssembly { get; set; }

    private static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\'
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly HashSet<string> managed = new HashSet<string>(PathComparer) { "LoadFolders.xml" };
    private string root;
    private string transaction;
    private string next;
    private string previous;

    public override bool Execute()
    {
        try
        {
            root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = root + Path.DirectorySeparatorChar;
            if (root.Length == 0 || PathComparer.Equals(prefix, Path.GetPathRoot(prefix)) ||
                Within(prefix, RepositoryRoot) || Within(RepositoryRoot, prefix) || Within(GameRoot, prefix))
                throw new IOException("Choose a mod directory outside the source repository, its ancestors and the game root.");

            foreach (ITaskItem item in Directories)
            {
                string path = Path.GetFullPath(item.ItemSpec);
                if (!PathComparer.Equals(Path.GetDirectoryName(path), root))
                    throw new IOException("Managed path is outside the mod directory: " + path);
                managed.Add(Path.GetFileName(path));
            }

            transaction = Path.Combine(Path.GetDirectoryName(root), "." + Path.GetFileName(root) + ".soilrelocation-deploy");
            next = Path.Combine(transaction, "next");
            previous = Path.Combine(transaction, "previous");
            // A process crash releases the mutex. The next invocation can then recover the journal.
            string key = Path.DirectorySeparatorChar == '\\' ? root.ToUpperInvariant() : root;
            using (var sha = SHA256.Create())
            using (var mutex = new Mutex(false, "SoilRelocation.Deploy." +
                       BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "")))
            {
                try { mutex.WaitOne(); }
                catch (AbandonedMutexException) { }
                try
                {
                    ValidatePathsAndOwner();
                    if (!ValidateOnly)
                    {
                        if (Directory.Exists(transaction))
                        {
                            RestorePreviousInstallation();
                            // The journal itself can still be open in Windows. Reuse it once its payload is gone.
                            ClearJournalContents();
                            ValidatePathsAndOwner();
                        }
                        else
                        {
                            CreateJournal();
                        }
                        Deploy();
                    }
                }
                finally { mutex.ReleaseMutex(); }
            }
            return true;
        }
        catch (Exception error)
        {
            Log.LogError(error.Message);
            return false;
        }
    }

    private static bool Within(string path, string parent)
    {
        string prefix = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static FileAttributes? Attributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void ValidateEntry(string path, bool directory)
    {
        FileAttributes? attributes = Attributes(path);
        if (!attributes.HasValue) return;
        if ((attributes.Value & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Deployment through symbolic links or junctions is not supported: " + path);
        if (attributes.Value.HasFlag(FileAttributes.Directory) != directory)
            throw new IOException("Unexpected file or directory at " + path);
    }

    private void ValidatePathsAndOwner()
    {
        for (var parent = new DirectoryInfo(root); parent != null; parent = parent.Parent)
            ValidateEntry(parent.FullName, true);
        if (Attributes(Path.Combine(root, ".git")).HasValue)
            throw new IOException("Deployment cannot replace content in a Git checkout.");
        foreach (string name in managed)
        {
            bool directory = name != "LoadFolders.xml";
            string path = Path.Combine(root, name);
            ValidateEntry(path, directory);
            if (directory && Directory.Exists(path)) ValidateManagedTree(path);
        }
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            var about = new XmlDocument { XmlResolver = null };
            string path = Path.Combine(root, "About", "About.xml");
            if (!File.Exists(path))
                throw new IOException("A nonempty destination must contain About/About.xml for " + PackageId + ".");
            about.Load(path);
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    about.SelectSingleNode("/ModMetaData/packageId")?.InnerText.Trim(), PackageId))
                throw new IOException("The destination belongs to another mod: " + root);
        }
        ValidateEntry(transaction, true);
        if (Directory.Exists(transaction))
        {
            string owner = Path.Combine(transaction, "owner");
            ValidateEntry(owner, false);
            if (!File.Exists(owner) || File.ReadAllText(owner) != PackageId)
                throw new IOException("The deployment transaction path is already occupied: " + transaction);
            ValidateEntry(next, true);
            ValidateEntry(previous, true);
        }
    }

    private static void ValidateManagedTree(string path)
    {
        foreach (string child in Directory.EnumerateFileSystemEntries(path))
        {
            FileAttributes attributes = File.GetAttributes(child);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Managed content contains a symbolic link or junction: " + child);
            if (attributes.HasFlag(FileAttributes.Directory)) ValidateManagedTree(child);
        }
    }

    private void CreateJournal()
    {
        // Publish an identifiable journal in one rename. An interruption during staging leaves the live root alone.
        string temporary = transaction + "." + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temporary);
        try
        {
            File.WriteAllText(Path.Combine(temporary, "owner"), PackageId);
            Directory.Move(temporary, transaction);
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    private void Deploy()
    {
        try
        {
            Directory.CreateDirectory(next);
            foreach (ITaskItem file in Files)
            {
                string target = Path.GetFullPath(Path.Combine(next, file.GetMetadata("PackagePath")));
                if (!Within(target, next)) throw new IOException("File escapes the staged package: " + target);
                CopyFile(file.ItemSpec, target);
            }
            CopyFile(CurrentAssembly, Path.Combine(next, "Current", "Assemblies", Path.GetFileName(CurrentAssembly)));
            string localId = Path.Combine("About", "PublishedFileId.txt");
            if (!File.Exists(Path.Combine(next, localId)) && File.Exists(Path.Combine(root, localId)))
                CopyFile(Path.Combine(root, localId), Path.Combine(next, localId));

            if (Directory.Exists(root))
            {
                string[] unmanaged = Directory.GetFileSystemEntries(root).Select(Path.GetFileName)
                    .Where(name => !managed.Contains(name)).ToArray();
                // Store exact names, including Unix filenames containing newlines, before the first live rename.
                File.WriteAllLines(Path.Combine(transaction, "unmanaged"),
                    unmanaged.Select(name => Convert.ToBase64String(Encoding.UTF8.GetBytes(name))));
                Directory.Move(root, previous);
                MoveUnmanaged(previous, next, unmanaged);
            }
            Directory.Move(next, root);
        }
        catch
        {
            RestorePreviousInstallation();
            TryRemoveJournal();
            throw;
        }
        // The new package is live. A cleanup failure must not turn publication into a failed deployment.
        TryRemoveJournal();
    }

    private static void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source, destination);
    }

    private static void MoveUnmanaged(string source, string destination, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            if (name.Length == 0 || name == "." || name == ".." || Path.GetFileName(name) != name)
                throw new IOException("Invalid unmanaged filename in deployment journal.");
            string entry = Path.Combine(source, name);
            FileAttributes? attributes = Attributes(entry);
            if (!attributes.HasValue) continue; // An interrupted transfer may have moved only a prefix of the list.
            string target = Path.Combine(destination, name);
            if (attributes.Value.HasFlag(FileAttributes.Directory))
                Directory.Move(entry, target);
            else
                File.Move(entry, target);
        }
    }

    private void RestorePreviousInstallation()
    {
        if (!Directory.Exists(previous)) return;
        if (Directory.Exists(next))
        {
            if (Attributes(root).HasValue)
                throw new IOException("The previous installation is preserved at " + previous +
                                      ", but its original path is occupied: " + root);
            string manifest = Path.Combine(transaction, "unmanaged");
            ValidateEntry(manifest, false);
            MoveUnmanaged(next, previous, File.ReadAllLines(manifest)
                .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line))));
            Directory.Move(previous, root);
        }
        else if (!Directory.Exists(root))
            throw new IOException("The published installation is missing. The previous package is at " + previous);
    }

    private void ClearJournalContents()
    {
        // Keep the owner marker at its known path until cleanup completes, even if a file is locked.
        if (Directory.Exists(previous)) Directory.Delete(previous, true);
        if (Directory.Exists(next)) Directory.Delete(next, true);
        File.Delete(Path.Combine(transaction, "unmanaged"));
    }

    private void TryRemoveJournal()
    {
        string cleanupPath = transaction;
        try
        {
            ClearJournalContents();
            string retired = transaction + "." + Guid.NewGuid().ToString("N");
            Directory.Move(transaction, retired);
            cleanupPath = retired;
            File.Delete(Path.Combine(retired, "owner"));
            Directory.Delete(retired);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            Log.LogWarning("Deployment cleanup deferred at " + cleanupPath + ": " + error.Message);
        }
    }
}