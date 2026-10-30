//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace SolAvesTools.Assets;

public enum AssetTypes : ulong
{                                                   // Magic  | XXH3Custom String
    Texture = 0xe207ff98b2318b34,                   // "PTEX" | phx:Texture
    Mesh = 0x3dfaf408fcf2fec8,                      // "PMSH" | phx:Mesh
    Material = 0xb935567b570e4fae,                  // "PMTR" | phx:Material
    Animation = 0xab98b88861a40aea,                 // "PANM" | phx:Animation
    SoundAudioSourceAsset = 0x148b3e57c0bc8b45,     // "PSND" | snd:SoundAudioSourceAsset
    Raw = 0x16ca37bd858404d5,                       // "RAW", | unknown class (no FlatBuffers root)
    AnimationSequenceAsset = 0xae0491d332cc870e,    // "PASQ" | phx:AnimationSequenceAsset
    Effect = 0x10262e81570397a8,                    // "PEFF" | phx:Effect
    Prefab = 0x6cbc35e7648d7a47,                    // "Pfab" | phx:Prefab
    Skeleton = 0x39cee52894585e7d,                  // "PSKT" | phx:Skeleton
    SkeletonSource = 0xc50178f3ea870ec8,            // "PSKS" | phx:SkeletonSource
    SoundToneAsset = 0x045fc7a9efa15ca4,            // "PSND" | snd:SoundToneAsset
    SoundCommandAsset = 0xec470e7b2e058081,         // "PSND" | snd:SoundCommandAsset
    AnimationList = 0x36cda108b09bfd33,             // "PANL" | phx:AnimationList
    SoundBankAsset = 0xfdac2382557f9fd4,            // "PSND" | snd:SoundBankAsset
    Scene2 = 0xeec3e237adc3068f,                    // "PSNE" | phx:Scene2
    AssetList = 0xe5c3c696eb579f5f,                 // "PALT" | phx:AssetList
    SoundGameInteractionAsset = 0x896489fbc8f21720, // "PSND" | snd:SoundGameInteractionAsset
    Shader = 0xffe88df8d27f503d,                    // "PSHD" | phx:Shader
    SoundSettingAsset = 0xaa8eaf61496e23af,         // "PSND" | snd:SoundSettingAsset
    AnimationMirror = 0x6ef3c87ef89c01b7,           // "PMIR" | phx:AnimationMirror
    DynamicBone = 0x08d6a1d8744bbf9a,               // "PDYB" | phx:DynamicBone
    RenderPipeline = 0xc98962375b836e17,            // "PRPL" | phx:RenderPipeline
    DynamicBoneBlend = 0xe80088299c827c62,          // "PDBB" | phx:DynamicBoneBlend
    ComposedFontAsset = 0x0ab09b99bb2a9737,         // "PCFT" | phx:ComposedFontAsset
    FontAsset = 0x6e8b7b461c0bf981,                 // "PFNT" | phx:FontAsset
    TextStyleAsset = 0x51272645a81a584b,            // "PSTY" | phx:TextStyleAsset
    SharedColor = 0xd0b0b6855b10bfb2,               // "PCOL" | phx:SharedColor
    ShaderGraph = 0xa518bd0ef7cb69c1,               // "PSHD" | phx:ShaderGraph
    SoundEffectAsset = 0x6a464e1047ac9626,          // "PSND" | snd:SoundEffectAsset
    SoundToneGroupAsset = 0x06b59c4aad782fba,       // "PSND" | snd:SoundToneGroupAsset
    Viewport = 0xd288116266722135,                  // "PVPT" | phx:Viewport
    SoundSettingPackageAsset = 0x123439f3a7277d0a,  // "PSND" | snd:SoundSettingPackageAsset
    SoundDeviceAsset = 0xa0d53089ac74f454,          // "PSND" | snd:SoundDeviceAsset
    SoundListenerAsset = 0x11b3e59f32171e02,        // "PSND" | snd:SoundListenerAsset
    ActiveGroupPreset = 0x01bdad3352e6b092,         // "PAGP" | phx:ActiveGroupPreset
    SoundAudioBusAsset = 0x2de54f1a149083d9,        // "PSND" | snd:SoundAudioBusAsset
    EffectScript = 0xb643842c3e7b9f5d,              // "PEFS" | phx:EffectScript
    SoundDebugPackageAsset = 0xb11e654ac62ddb5b,    // "PSND" | snd:SoundDebugPackageAsset
    PhysicsMaterial = 0xd5ce6d9fa0591a73,           // "PPHM" | phx:PhysicsMaterial
}

public class AssetTypeToExtension
{
    public static readonly Dictionary<AssetTypes, string> Extensions = new()
    {
        [AssetTypes.Texture] = "ptexj",
        [AssetTypes.Mesh] = "pmshj",
        [AssetTypes.Material] = "pmtrj",
        [AssetTypes.Animation] = "panmj",
        [AssetTypes.SoundAudioSourceAsset] = "psndsource",
        [AssetTypes.Raw] = "raw",
        [AssetTypes.AnimationSequenceAsset] = "pasqj",
        [AssetTypes.Effect] = "peffj",                                // Guessed 
        [AssetTypes.Prefab] = "pprfj",
        [AssetTypes.Skeleton] = "psktj",
        [AssetTypes.SkeletonSource] = "psksj",
        [AssetTypes.SoundToneAsset] = "psndtone",
        [AssetTypes.SoundCommandAsset] = "pscm",
        [AssetTypes.AnimationList] = "panlj",
        [AssetTypes.SoundBankAsset] = "psbn",
        [AssetTypes.Scene2] = "pscenej",
        [AssetTypes.AssetList] = "assetlist",
        [AssetTypes.SoundGameInteractionAsset] = "psndinteraction",   // Guessed    
        [AssetTypes.Shader] = "pshdj",
        [AssetTypes.SoundSettingAsset] = "psndsettings",              // Guessed    
        [AssetTypes.AnimationMirror] = "panmmirror",                  // Guessed    
        [AssetTypes.DynamicBone] = "pdybj",                           // Guessed       
        [AssetTypes.RenderPipeline] = "prplj",                        // Guessed      
        [AssetTypes.DynamicBoneBlend] = "pdbbj",                      // Guessed          
        [AssetTypes.ComposedFontAsset] = "pcftj",
        [AssetTypes.FontAsset] = "pfntj",
        [AssetTypes.TextStyleAsset] = "pstyj",
        [AssetTypes.SharedColor] = "pcolj",
        [AssetTypes.ShaderGraph] = "pshgj",                           // Guessed               
        [AssetTypes.SoundEffectAsset] = "psndeffect",
        [AssetTypes.SoundToneGroupAsset] = "psndtonegroup",           // Guessed
        [AssetTypes.Viewport] = "pvptj",                              // Guessed                 
        [AssetTypes.SoundSettingPackageAsset] = "psndsettingpackage", // Guessed  
        [AssetTypes.SoundDeviceAsset] = "psdv",                       // Guessed          
        [AssetTypes.SoundListenerAsset] = "psls",                     // Guessed        
        [AssetTypes.ActiveGroupPreset] = "pagpj",                     // Guessed         
        [AssetTypes.SoundAudioBusAsset] = "psab",                     // Guessed        
        [AssetTypes.EffectScript] = "pefsj",                          // Guessed              
        [AssetTypes.SoundDebugPackageAsset] = "psnddebug",            // Guessed
        [AssetTypes.PhysicsMaterial] = "pphmj",                       // Guessed           
    };
}