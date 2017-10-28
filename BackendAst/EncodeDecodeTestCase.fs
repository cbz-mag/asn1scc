﻿module EncodeDecodeTestCase
open System
open System.Numerics
open System.Globalization
open System.IO

open FsUtils
open CommonTypes
open Asn1AcnAst
open Asn1Fold
open Asn1AcnAstUtilFunctions
open DAst
open DAstUtilFunctions

let getFuncName (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (sEncoding:string) (typeId:ReferenceToType) =
    typeId.tasInfo |> Option.map (fun x -> ToC2(r.args.TypePrefix + x.tasName + "_" + sEncoding + "enc_dec"))


type StatementKind =
    //|Update_DecIn     of AcnTypes.AcnParameter       
    |Encode_input
    |Decode_output
    |Validate_output
    |Compare_input_output


let OptFlatMap fun1 u =
    match u with
    | None  -> None
    | Some uu ->
       match uu with
       | None   -> None
       | Some uuu -> fun1 uuu

let rec getAmberIsValid (t:Asn1AcnAst.Asn1Type) = 
    match t.Kind with
    | Asn1AcnAst.Integer      _ ->  ""
    | Asn1AcnAst.Real         _ -> ""
    | Asn1AcnAst.IA5String    _ -> ""
    | Asn1AcnAst.NumericString _ -> ""
    | Asn1AcnAst.OctetString  _ -> "&"
    | Asn1AcnAst.NullType     _ -> ""
    | Asn1AcnAst.BitString    _ -> "&"
    | Asn1AcnAst.Boolean      _ -> ""
    | Asn1AcnAst.Enumerated   _ -> ""
    | Asn1AcnAst.SequenceOf   _ -> "&"
    | Asn1AcnAst.Sequence     _ -> "&"
    | Asn1AcnAst.Choice       _ -> "&"
    | Asn1AcnAst.ReferenceType z -> getAmberIsValid z.baseType

let rec getAmberDecode (t:Asn1AcnAst.Asn1Type) = 
    match t.Kind with
    | Asn1AcnAst.IA5String    _ -> ""
    | Asn1AcnAst.NumericString _ -> ""
    | Asn1AcnAst.ReferenceType z -> getAmberIsValid z.baseType
    | _                          -> "&"

let createUperEncDecFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (typeDefinition:TypeDefinitionCommon) (eqFunc:EqualFunction) (isValidFunc: IsValidFunction option) (encFunc : UPerFunction option) (decFunc : UPerFunction option)   (us:State)  =
    let sEnc = ""
    let funcName            = getFuncName r l sEnc t.id

    let printCodec_body = match l with C -> test_cases_c.PrintCodec_body   | Ada -> test_cases_c.PrintCodec_body
    let printCodec_body_header = match l with C -> test_cases_c.PrintCodec_body_header   | Ada -> test_cases_c.PrintCodec_body_header
    let joinItems = match l with C -> test_cases_c.JoinItems   | Ada -> test_cases_c.JoinItems

    let p : FuncParamType = t.getParamType l Encode
    let varName = p.p
    let sStar = p.getStar l
    let sAmberDecode = getAmberDecode t
    let sAmberIsValid = getAmberIsValid t
   
    match funcName  with
    | None              -> None, us
    | Some funcName     -> 
        
        let printStatement stm sNestedContent = 
            let encode = match l with C -> test_cases_c.Codec_Encode   | Ada -> test_cases_c.Codec_Encode
            let decode = match l with C -> test_cases_c.Codec_Decode   | Ada -> test_cases_c.Codec_Decode
            let validateOutput = match l with C -> test_cases_c.Codec_validate_output   | Ada -> test_cases_c.Codec_validate_output
            let compareInputWithOutput = match l with C -> test_cases_c.Codec_compare_input_with_output   | Ada -> test_cases_c.Codec_compare_input_with_output
            let content= 
                match stm with
                |Encode_input           -> option {
                                                let! encF = encFunc
                                                let! encFunName = encF.funcName
                                                return encode encFunName varName
                                           }
                |Decode_output          -> option {
                                                let! decF = decFunc
                                                let! decFunName = decF.funcName
                                                return decode decFunName typeDefinition.name sEnc sAmberDecode 
                                           }
                    
                |Validate_output        -> 
                                           option {
                                                let! f = isValidFunc
                                                let! fname = f.funcName
                                                return validateOutput fname sAmberIsValid
                                           }
                |Compare_input_output   -> 
                                           option {
                                                let! fname = eqFunc.isEqualFuncName
                                                return compareInputWithOutput fname varName sAmberIsValid
                                           }                
            joinItems (content.orElse "") sNestedContent


        let sNestedStatements = 
            let rec printStatements statements : string option = 
                match statements with
                |[]     -> None
                |x::xs  -> 
                    match printStatements xs with
                    | None                 -> Some (printStatement x  None)
                    | Some childrenCont    -> Some (printStatement x  (Some childrenCont))

            printStatements [Encode_input; Decode_output; Validate_output; Compare_input_output]

        let func = printCodec_body funcName typeDefinition.name sStar varName sEnc (sNestedStatements.orElse "")
        let funcDef = printCodec_body_header funcName typeDefinition.name sStar varName
        let ret = 
            {
                EncodeDecodeTestFunc.funcName   = funcName
                func                            = func 
                funcDef                         = funcDef
            }
        Some ret, us


let createAcnEncDecFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (typeDefinition:TypeDefinitionCommon) (eqFunc:EqualFunction) (isValidFunc: IsValidFunction option) (encFunc : AcnFunction option) (decFunc : AcnFunction option)   (us:State)  =
    let sEnc = "ACN_"

    let funcName            = getFuncName r l sEnc t.id

    let printCodec_body = match l with C -> test_cases_c.PrintCodec_body   | Ada -> test_cases_c.PrintCodec_body
    let printCodec_body_header = match l with C -> test_cases_c.PrintCodec_body_header   | Ada -> test_cases_c.PrintCodec_body_header
    let joinItems = match l with C -> test_cases_c.JoinItems   | Ada -> test_cases_c.JoinItems

    let p : FuncParamType = t.getParamType l Encode
    let varName = p.p
    let sStar = p.getStar l
    let sAmberDecode = getAmberDecode t
    let sAmberIsValid = getAmberIsValid t

    match hasAcnEncodeFunction encFunc t.acnParameters  with
    | false -> None, us
    | true  ->
        match funcName  with
        | None              -> None, us
        | Some funcName     -> 
            let printStatement stm sNestedContent = 
                let encode = match l with C -> test_cases_c.Codec_Encode   | Ada -> test_cases_c.Codec_Encode
                let decode = match l with C -> test_cases_c.Codec_Decode   | Ada -> test_cases_c.Codec_Decode
                let validateOutput = match l with C -> test_cases_c.Codec_validate_output   | Ada -> test_cases_c.Codec_validate_output
                let compareInputWithOutput = match l with C -> test_cases_c.Codec_compare_input_with_output   | Ada -> test_cases_c.Codec_compare_input_with_output
                let content= 
                    match stm with
                    |Encode_input           -> option {
                                                    let! encF = encFunc
                                                    let! encFunName = encF.funcName
                                                    return encode encFunName varName
                                               }
                    |Decode_output          -> option {
                                                    let! decF = decFunc
                                                    let! decFunName = decF.funcName
                                                    return decode decFunName typeDefinition.name sEnc sAmberDecode 
                                               }
                    
                    |Validate_output        -> 
                                               option {
                                                    let! f = isValidFunc
                                                    let! fname = f.funcName
                                                    return validateOutput fname sAmberIsValid
                                               }
                    |Compare_input_output   -> 
                                               option {
                                                    let! fname = eqFunc.isEqualFuncName
                                                    return compareInputWithOutput fname varName sAmberIsValid
                                               }                
                joinItems (content.orElse "") sNestedContent

            let sNestedStatements = 
                let rec printStatements statements : string option = 
                    match statements with
                    |[]     -> None
                    |x::xs  -> 
                        match printStatements xs with
                        | None                 -> Some (printStatement x  None)
                        | Some childrenCont    -> Some (printStatement x  (Some childrenCont))

                printStatements [Encode_input; Decode_output; Validate_output; Compare_input_output]

            let func = printCodec_body funcName typeDefinition.name sStar varName sEnc (sNestedStatements.orElse "")
            let funcDef = printCodec_body_header funcName typeDefinition.name sStar varName
        
            let ret = 
                {
                    EncodeDecodeTestFunc.funcName   = funcName
                    func                            = func 
                    funcDef                         = funcDef
                }
            Some ret, us



(*
Automatic Test case values

*)




let foldGenericCon (l:ProgrammingLanguage) (c:GenericConstraint<'v>)  =
    foldGenericConstraint
        (fun e1 e2 b s      -> e1@e2, s)
        (fun e1 e2 s        -> e1@e2, s)
        (fun e s            -> [], s)
        (fun e1 e2 s        -> e1, s)
        (fun e s            -> e, s)
        (fun e1 e2 s        -> e1@e2, s)
        (fun v  s           -> [v] ,s)
        c
        0 |> fst 



let foldRangeCon  getNext getPrev min max zero (c:RangeTypeConstraint<'v1,'v1>)  =
    foldRangeTypeConstraint        
        (fun e1 e2 b s      -> e1@e2, s)    //union
        (fun e1 e2 s        -> e1@e2, s)    //Intersection
        (fun e s            -> [], s)       //AllExcept
        (fun e1 e2 s        -> e1, s)       //ExceptConstraint
        (fun e s            -> e, s)        //RootConstraint
        (fun e1 e2 s        -> e1@e2, s)    //RootConstraint2
        (fun v  s         -> [v] ,s)        // SingleValueConstraint
        (fun v1 v2  minIsIn maxIsIn s   ->  //RangeContraint
            [(if minIsIn then v1 else (getNext v1));(if maxIsIn then v2 else (getPrev v2))], s)
        (fun v1 minIsIn s   -> [(if minIsIn then v1 else (getNext v1)); max], s) //Contraint_val_MAX
        (fun v2 maxIsIn s   -> [min; (if maxIsIn then v2 else (getPrev v2))], s) //Contraint_MIN_val
        c
        0 |> fst 


let IntegerAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Integer) =
    let allCons = DAstValidate.getIntSimplifiedConstraints r o.isUnsigned o.AllCons
    let min = r.args.IntMin o.isUnsigned
    let max = r.args.IntMax o.isUnsigned
    let getNext a = match a < max with true -> a + 1I | false -> max
    let getPrev a = match a > min with true -> a - 1I | false -> min
    match allCons with
    | []    -> [min; 0I; max] |> Seq.distinct |> Seq.toList
    | _     -> 
        let ret = allCons |> List.collect (foldRangeCon  getNext getPrev min max 0I ) |> Seq.distinct |> Seq.toList
        let aaa = ret |> List.filter (isValidValueRanged allCons)
        aaa

let RealAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Real) =
    let allCons = o.AllCons
    let min = Double.MinValue
    let max = Double.MaxValue
    match allCons with
    | []    -> [min; 0.0; max] 
    | _     -> 
        allCons |> List.collect (foldRangeCon id id min max 0.0 ) |> Seq.distinct |> Seq.toList

let EnumeratedAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Enumerated) =
    let allItems = o.items |> List.map(fun z -> z.Name.Value)
    match o.AllCons with
    | [] -> allItems
    | _  -> allItems |> List.filter (isValidValueGeneric o.AllCons (=))
    
let BooleanAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Boolean) =
    let allItems = [true; false]
    match o.AllCons with
    | [] -> allItems
    | _  -> allItems |> List.filter (isValidValueGeneric o.AllCons (=))
    

let maxItems = 100000
let StringAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.StringType) =
    match o.minSize > maxItems with
    | true  -> []   // the generated string will be very large
    | false ->  
        match o.uperCharSet |> Seq.filter(fun c -> not (System.Char.IsControl c)) |> Seq.toList with
        | chr::_    -> 
            let s1 = System.String(chr, o.minSize) 
            match o.minSize = o.maxSize  || o.maxSize > maxItems with
            | true  -> [s1] 
            | false ->
                let s2 = System.String(chr, o.maxSize) 
                [s1;s2]
        | []        -> []

let OctetStringAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.OctetString) =
    match o.minSize > maxItems with
    | true  -> []   // the generated string will be very large
    | false ->  
        let s1 = [1 .. o.minSize] |> List.map (fun i -> 0uy)
        match o.minSize = o.maxSize  || o.maxSize > maxItems with
        | true  -> [s1] 
        | false ->
            let s2 = [1 .. o.maxSize] |> List.map (fun i -> 0uy)
            [s1;s2]

let BitStringAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.BitString) =
    match o.minSize > maxItems with
    | true  -> []   // the generated string will be very large
    | false ->  
        let s1 = System.String('0', o.minSize)
        match o.minSize = o.maxSize  || o.maxSize > maxItems with
        | true  -> [s1] 
        | false ->
            let s2 = System.String('0', o.maxSize)
            [s1;s2]

let SequenceOfAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.SequenceOf) (childType:Asn1Type) =
    match o.minSize > maxItems with
    | true  -> []   // the generated string will be very large
    | false ->  
        let generateValue (childVal:Asn1Value) =
            let s1 = [1 .. o.minSize] |> List.map (fun i -> childVal)
            match o.minSize = o.maxSize  || o.maxSize > maxItems with
            | true  -> [s1] 
            | false ->
                let s2 = [1 .. o.maxSize] |> List.map (fun i -> childVal)
                [s1;s2]
        childType.automaticTestCasesValues |> List.collect generateValue
(*
let rec permutation (a:int list list) =
    match a with
    | []    -> [[]]
    | a1::xs ->
        let rest = permutation xs
        seq {
            for i1 in a1 do
                for subList in rest do
                    yield i1::subList
        } |> Seq.toList
*)

let SequenceAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Sequence) (children:SeqChildInfo list) =
    let asn1Children = 
        children |> 
        List.choose(fun c -> match c with Asn1Child x -> Some x | _ -> None) |> 
        List.filter(fun z -> match z.Optionality with Some Asn1AcnAst.AlwaysAbsent -> false | _ -> true)
    let rec generateCases (children : Asn1Child list) =
        match children with
        | []        -> [[]]
        | x1::xs    -> 
            let ths = x1.Type.automaticTestCasesValues |> List.map(fun v -> {NamedValue.name = x1.Name.Value; Value = v}) 

            let rest = generateCases xs
            seq {
//                match x1.Optionality with
//                | Some (Asn1AcnAst.Optional _)    -> yield! rest
//                | _                               -> 
                    for i1 in ths do    
                        for lst in rest do
                            yield i1::lst
            } |> Seq.toList 
    generateCases asn1Children

let ChoiceAutomaticTestCaseValues (r:Asn1AcnAst.AstRoot)  (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Choice) (children:ChChildInfo list) =
    seq {
        for ch in children do
            for v in ch.chType.automaticTestCasesValues do
                yield {NamedValue.name = ch.Name.Value; Value = v}
    } |> Seq.toList