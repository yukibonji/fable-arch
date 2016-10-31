[<RequireQualifiedAccess>]
module Fable.Arch.Virtualdom

open Fable.Core
open Fable.Import.Browser
open Fable.Core.JsInterop

open Html
open App

[<Import("h","virtual-dom")>]
let h(arg1: string, arg2: obj, arg3: obj[]): obj = failwith "JS only"

[<Import("diff","virtual-dom")>]
let diff (tree1:obj) (tree2:obj): obj = failwith "JS only"

[<Import("patch","virtual-dom")>]
let patch (node:obj) (patches:obj): Fable.Import.Browser.Node = failwith "JS only"

[<Import("create","virtual-dom")>]
let createElement (e:obj): Fable.Import.Browser.Node = failwith "JS only"

let createTree<'T> (handler:'T -> unit) tag (attributes:Attribute<'T> list) children =
    let toAttrs (attrs:Attribute<'T> list) =
        let (elAttributes, props) = 
            attrs
            |> List.fold (fun (elAttrs, props) a ->
                match a with
                | Attribute (k,v) -> ((k ==> v)::elAttrs,props)
                | Style style -> (elAttrs, ("style" ==> createObj(unbox style))::props)
                | Property (k,v) -> (elAttrs, (k ==> v)::props)
                | EventHandler(ev,f) -> (elAttrs, (ev ==> ((f >> handler) :> obj))::props)
            ) ([],[])

        match elAttributes with
        | [] -> props
        | x -> ("attributes" ==> (createObj(x)))::props
        |> createObj
    let elem = h(tag, toAttrs attributes, List.toArray children)
    elem

type RenderState = 
    | NoRequest
    | PendingRequest
    | ExtraRequest

type ViewState<'TMessage> = 
    {
        CurrentTree: obj
        NextTree: obj
        Node: Node 
        RenderState: RenderState
    }

let rec renderSomething handler node = 
    match node with
    | Element((tag,attrs), nodes)
    | Svg((tag,attrs), nodes) -> createTree handler tag attrs (nodes |> List.map (renderSomething handler))
    | VoidElement (tag, attrs) -> createTree handler tag attrs []
    | Text str -> box(string str)
    | WhiteSpace str -> box(string str)

let render handler view viewState =
    let tree = renderSomething handler view
    {viewState with NextTree = tree}

let createRender selector handler view =
    let node = document.body.querySelector(selector) :?> HTMLElement
    let tree = renderSomething handler view
    let vdomNode = createElement tree
    node.appendChild(vdomNode) |> ignore
    let mutable viewState = 
        {
            CurrentTree = tree
            NextTree = tree
            Node = vdomNode
            RenderState = NoRequest
        }

    let raf cb = 
        Fable.Import.Browser.window.requestAnimationFrame(fun fb -> cb())

    let render' handler view = 
        let viewState' = render handler view viewState
        viewState <- viewState'

        let rec callBack() = 
            match viewState.RenderState with
            | PendingRequest ->
                raf callBack |> ignore
                viewState <- {viewState with RenderState = ExtraRequest}

                let patches = diff viewState.CurrentTree viewState.NextTree
                patch viewState.Node patches |> ignore
                viewState <- {viewState with CurrentTree = viewState.NextTree}
            | ExtraRequest -> 
                viewState <- {viewState with RenderState = NoRequest}
            | NoRequest -> raise (exn "Shouldn't happen")
        
        match viewState.RenderState with
        | NoRequest ->
            raf callBack |> ignore
        | _ -> ()
        viewState <- {viewState with RenderState = PendingRequest}

    render'
