// SPDX-FileCopyrightText: 2026 BlackM Project
// SPDX-License-Identifier: CC-BY-SA-3.0

using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Kovak;

/// <summary>
/// Событие переключения забрала шлема КОВАК (клиент → сервер).
/// </summary>
[Serializable, NetSerializable]
public sealed class KovakHelmetToggleMessage : EntityEventArgs
{
    public NetEntity Helmet;

    public KovakHelmetToggleMessage(NetEntity helmet)
    {
        Helmet = helmet;
    }
}
