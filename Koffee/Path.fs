﻿namespace Koffee

open System.Text.RegularExpressions
open Utility

type IOPath = System.IO.Path

type PathFormat =
    | Windows
    | Unix
    override this.ToString() = sprintf "%A" this

type Path private (path: string) =
    static let firstModify f (s: string) =
        if System.String.IsNullOrEmpty(s) then ""
        else
            let first = string s.[0] |> f
            if s.Length > 1 then first + s.Substring(1)
            else first
    static let firstToUpper = firstModify (fun s -> s.ToUpper())
    static let firstToLower = firstModify (fun s -> s.ToLower())

    static let isMatch pattern s = Regex.IsMatch(s, pattern, RegexOptions.IgnoreCase)

    static let noInvalidChars (s: string) =
        IOPath.GetInvalidPathChars()
        |> Seq.append "?*"
        |> Seq.exists (fun c -> Seq.contains c s)
        |> not

    static let normalize (pathStr: string) =
        pathStr
            .Replace("/", @"\").Trim('\\')
            .Replace(":", "").Insert(1, ":")
            |> (fun s -> if s.EndsWith(":") then s + @"\" else s)
            |> firstToUpper
            |> Path

    static let (|WinPath|_|) s =
        if isMatch @"^[a-z]:([/\\]|$)" s && noInvalidChars s then
            Some (normalize s)
        else None

    static let (|UnixPath|_|) s =
        if isMatch @"^/[a-z]([/\\]|$)" s && noInvalidChars s then
            Some (normalize s)
        else None

    static let root = ""
    static let rootWindows = "Drives"
    static let rootUnix = "/"
    static let isRoot p =
        p = root || p = rootUnix || System.String.Equals(p, rootWindows, System.StringComparison.OrdinalIgnoreCase)

    static member Root = Path root

    static member Parse (s: string) =
        match s.Trim() with
        | p when isRoot p -> Some (Path root)
        | WinPath path | UnixPath path -> Some path
        | _ -> None

    static member SplitName (name: string) =
        match name.LastIndexOf('.') with
        | -1 -> (name, "")
        | index -> (name.Substring(0, index), name.Substring(index))

    member private this.Value = path

    member this.Format fmt =
        match fmt with
        | Windows ->
            if path = root then rootWindows
            else path
        | Unix ->
            if path = root then rootUnix
            else
                (path |> firstToLower).Replace(":", "").Insert(0, "/").Replace(@"\", "/")

    member this.Name =
        path |> IOPath.GetFileName

    member this.NameAndExt = Path.SplitName this.Name

    member this.Drive =
        match path with
        | p when p.Length > 0 && System.Char.IsLetter p.[0] -> Some (string p.[0])
        | _ -> None

    member this.Parent =
        if path = root then
            Path.Root
        else
            IOPath.GetDirectoryName path
            |> Option.ofObj
            |> Option.coalesce root
            |> Path

    member this.Join name =
        IOPath.Combine(path, name) |> Path

    override this.Equals other =
        match other with
        | :? Path as p -> this.Value = p.Value
        | _ -> false

    override this.GetHashCode() = this.Value.GetHashCode()

    // for debugging
    override this.ToString() = this.Format Windows
