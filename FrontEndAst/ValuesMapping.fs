﻿module ValuesMapping

open System
open System.Numerics
open FsUtils
open Asn1AcnAst

let getBaseValue (r:Asn1Ast.AstRoot) (v:Asn1Ast.Asn1Value) =
    match v.Kind with
    | Asn1Ast.RefValue(md,ts)  -> 
        match r.Modules |> Seq.tryFind(fun m -> m.Name = md) with
        | Some m ->
            match m.ValueAssignments |> Seq.tryFind (fun v -> v.Name = ts) with
            | Some v -> v.Value
            | None -> raise (SemanticError(md.Location, (sprintf "Invalid value reference %s.%s" md.Value ts.Value)))
        | None  -> raise (SemanticError(md.Location, (sprintf "Invalid value reference %s.%s" md.Value ts.Value)))
    | _     -> v

let rec getActualBaseValue (r:Asn1Ast.AstRoot) (v:Asn1Ast.Asn1Value) =
    match v.Kind with
    | Asn1Ast.RefValue(md,ts)  -> 
        match r.Modules |> Seq.tryFind(fun m -> m.Name = md) with
        | Some m ->
            match m.ValueAssignments |> Seq.tryFind (fun v -> v.Name = ts) with
            | Some v -> getActualBaseValue r v.Value
            | None -> raise (SemanticError(md.Location, (sprintf "Invalid value reference %s.%s" md.Value ts.Value)))
        | None  -> raise (SemanticError(md.Location, (sprintf "Invalid value reference %s.%s" md.Value ts.Value)))
    | _     -> v


let rec mapValue 
    (r:Asn1Ast.AstRoot)
    (t:Asn1Ast.Asn1Type)
    (v:Asn1Ast.Asn1Value) =
    let baseType = Asn1Ast.GetActualType  t r
    let valueKind = 
        match v.Kind with
        | Asn1Ast.IntegerValue      v -> 
            match baseType.Kind with
            | Asn1Ast.Integer   -> IntegerValue v
            | Asn1Ast.Real     ->  RealValue ({ Value = (double v.Value); Location = v.Location})
            | _                 -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found an INTEGER value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.RealValue         v -> 
            match baseType.Kind with
            | Asn1Ast.Real     ->  RealValue v
            | _                 -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found a REAL value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.StringValue       v -> 
            match baseType.Kind with
            | Asn1Ast.IA5String     ->  StringValue v
            | Asn1Ast.NumericString ->  StringValue v
            | _                 -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found a STRING value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.BooleanValue      v -> 
            match baseType.Kind with
            | Asn1Ast.Boolean     ->      BooleanValue v
            | _                 -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found a BOOLEAN value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.BitStringValue    v -> 
            match baseType.Kind with
            | Asn1Ast.BitString ->  BitStringValue v
            | _                 -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found a BIT STRING value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.OctetStringValue  bv -> 
            match baseType.Kind with
            | Asn1Ast.OctetString ->  OctetStringValue bv
            | _                   -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found an OCTET STRING value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.RefValue    (md,ts) -> 
            let resolveReferenceValue md ts =
                let newVal = mapValue r t (getBaseValue r v)
                RefValue ((md,ts), newVal)
            let baseType = Asn1Ast.GetActualType  t r
            match baseType.Kind with
            | Asn1Ast.Enumerated (items)    ->
                match items |> Seq.tryFind (fun i -> i.Name = ts ) with
                | Some ni -> EnumValue ni.Name
                | _       -> resolveReferenceValue md ts
            | _                             -> resolveReferenceValue md ts
        | Asn1Ast.SeqOfValue        chVals -> 
            match baseType.Kind with
            | Asn1Ast.SequenceOf chType ->
                let chValue = chVals |> List.map (mapValue r chType)
                SeqOfValue chValue
            | _                         -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found a SEQUENCE OF value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.SeqValue       chVals -> 
            match baseType.Kind with
            | Asn1Ast.Sequence children ->
                let chValue = 
                    chVals |> 
                    List.map (fun (cnm, chv) -> 
                        match children |> Seq.tryFind (fun c -> c.Name = cnm) with
                        | Some chType -> 
                            let chValue = mapValue r chType.Type chv
                            {NamedValue.name  = cnm; Value = chValue}
                        | None        -> raise (SemanticError(v.Location, (sprintf "No child exists with name '%s' " cnm.Value))) )
                SeqValue chValue
            | _                         -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found a SEQUENCE value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.ChValue          (cnm, chv) -> 
            match baseType.Kind with
            | Asn1Ast.Choice children ->
                match children |> Seq.tryFind (fun c -> c.Name = cnm) with
                | Some chType -> 
                    let chValue = mapValue r chType.Type chv
                    ChValue ({NamedValue.name  = cnm; Value = chValue})
                | None        -> raise (SemanticError(v.Location, (sprintf "No child exists with name %s" cnm.Value))) 
            | _                         -> raise (SemanticError(v.Location, (sprintf "Expecting a %s value but found a SEQUENCE value" (Asn1Ast.getASN1Name r baseType))))
        | Asn1Ast.NullValue           -> NullValue ()

    {Asn1Value.kind = valueKind; id = v.id; loc = v.Location}    