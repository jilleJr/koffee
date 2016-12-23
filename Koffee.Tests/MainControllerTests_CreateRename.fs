﻿namespace Koffee.Tests

open System
open System.Windows.Input
open FSharp.Desktop.UI
open NUnit.Framework
open FsUnitTyped
open Foq
open KellermanSoftware.CompareNetObjects
open Koffee
open Testing

[<TestFixture>]
type ``MainController tests for creating and renaming files and folders``() =
    let oldNodes = [
        {Path = Path "path1"; Name = "one"; Type = Folder; Modified = None; Size = None}
        {Path = Path "path2"; Name = "two"; Type = Folder; Modified = None; Size = None}
    ]

    let newNodes = [
        {Path = Path "path/new one"; Name = "new one"; Type = Folder; Modified = None; Size = None}
        {Path = Path "path/new two"; Name = "new two"; Type = Folder; Modified = None; Size = None}
    ]

    let CreateModel () =
        let model = Model.Create<MainModel>()
        model.Path <- Path "path"
        model.Nodes <- oldNodes
        model.Cursor <- 0
        model.CommandText <- ""
        model.CommandTextSelection <- (1, 1)
        model.BackStack <- [Path "back", 8]
        model.ForwardStack <- [Path "fwd", 9]
        model

    let CreateFileSys () =
        Mock<IFileSystemService>()
            .Setup(fun x -> <@ x.GetNodes (Path "path") @>).Returns(newNodes)
            .Create()

    let CreateUnauthorizedFileSys () =
        let path = Path "path"
        Mock<IFileSystemService>()
            .Setup(fun x -> <@ x.GetNodes path @>).Returns(newNodes)
            .Setup(fun x -> <@ x.Create (any()) path (any()) @>).Raises<UnauthorizedAccessException>()
            .Setup(fun x -> <@ x.Rename (any()) (any()) @>).Raises<UnauthorizedAccessException>()
            .Create()

    let CreateController fileSys =
        let settingsFactory () = Mock.Of<Mvc<SettingsEvents, SettingsModel>>()
        MainController(fileSys, settingsFactory)

    let RenameTextSelection cursorPosition fileName =
        let fileSys = CreateFileSys()
        let contr = CreateController fileSys
        let model = CreateModel()
        let node = {Path = Path "path3"; Name = fileName; Type = File; Modified = None; Size = None}
        model.Nodes <- List.append oldNodes [node]
        model.Cursor <- model.Nodes.Length - 1
        contr.StartInput (Rename cursorPosition) model

        model.CommandInputMode |> shouldEqual (Some (Rename cursorPosition))
        model.CommandText |> shouldEqual node.Name
        model.CommandTextSelection

    let Comparer () =
        CompareLogic() |> ignoreMembers ["CommandText"]

    [<Test>]
    member x.``Create folder calls fileSys.Create, reloads nodes and sets cursor``() =
        let fileSys = CreateFileSys()
        let contr = CreateController fileSys
        let model = CreateModel()
        model.CommandInputMode <- Some CreateFolder
        model.CommandText <- "new two"
        contr.ExecuteCommand model

        let nodeType = Folder
        let path = model.Path
        verify <@ fileSys.Create nodeType path "new two" @> once
        let expected = CreateModel()
        expected.Nodes <- newNodes
        expected.Cursor <- 1
        expected.Status <- MainController.CreateItemStatus Folder "new two"
        Comparer() |> assertAreEqualWith expected model

    [<Test>]
    member x.``Create folder handles error by setting error status``() =
        let fileSys = CreateUnauthorizedFileSys()
        let contr = CreateController fileSys
        let model = CreateModel()
        model.CommandInputMode <- Some CreateFolder
        model.CommandText <- "new two"
        contr.ExecuteCommand model

        let expected = CreateModel()
        expected.IsErrorStatus <- true
        Comparer()
            |> ignoreMembers ["Status"]
            |> assertAreEqualWith expected model

    [<Test>]
    member x.``Rename calls fileSys.Rename, reloads nodes and sets cursor``() =
        let fileSys = CreateFileSys()
        let contr = CreateController fileSys
        let model = CreateModel()
        model.Cursor <- 1
        model.CommandInputMode <- Some (Rename Begin)
        model.CommandText <- "new two"
        contr.ExecuteCommand model

        let nodeType = Folder
        let path = oldNodes.[1].Path
        verify <@ fileSys.Rename nodeType path "new two" @> once
        let expected = CreateModel()
        expected.Nodes <- newNodes
        expected.Cursor <- 1
        expected.Status <- MainController.RenameStatus "two" "new two"
        Comparer() |> assertAreEqualWith expected model

    [<Test>]
    member x.``Rename handles error by setting error status``() =
        let fileSys = CreateUnauthorizedFileSys()
        let contr = CreateController fileSys
        let model = CreateModel()
        model.Cursor <- 1
        model.CommandInputMode <- Some (Rename Begin)
        model.CommandText <- "new two"
        contr.ExecuteCommand model

        let expected = CreateModel()
        expected.Cursor <- 1
        expected.IsErrorStatus <- true
        Comparer()
            |> ignoreMembers ["Status"]
            |> assertAreEqualWith expected model

    [<Test>]
    member x.``StartInput for rename at beginning sets command text and selection``() =
        RenameTextSelection Begin "three.txt.old" |> shouldEqual (0, 0)

    [<Test>]
    member x.``StartInput for rename at end sets command text and selection``() =
        RenameTextSelection End "three.txt.old" |> shouldEqual (9, 0)

    [<Test>]
    member x.``StartInput for rename replace sets command text and selection``() =
        RenameTextSelection Replace "three.txt.old" |> shouldEqual (0, 9)
