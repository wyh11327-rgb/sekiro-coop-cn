//! Unlimited Spirit Emblems (`unlimited_spirit_emblems`, on by default).
//!
//! Sekiro keeps a set of debug switches in a static block near WorldChrMan. One of them is
//! `PlayerNoEmblemsConsume`: while its byte is 1 the game does not take a Spirit Emblem when a
//! prosthetic tool or ninjutsu is used. The offset is the same one SekiroTool uses for 1.06
//! (`DebugFlags.Base = module + 0x3D7A366`, `PlayerNoEmblemsConsume = +0x4`); it is not a hook,
//! so it cannot conflict with the game's own code.
//!
//! The block lives in the exe's static data and survives loading screens, but another tool (or
//! the debug menu) can clear it. `tick` therefore checks it about once a second and only writes
//! when the value differs, so turning the setting off in the ini restores normal consumption.
//!
//! Everything here runs on the frame task, like the rest of the mod.

use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};

use crate::{game, log};

/// `DebugFlags` block of Sekiro 1.06 (same value as SekiroTool's `Offsets.DebugFlags.Base`).
const DEBUG_FLAGS_RVA: usize = 0x3D7A366;
/// `PlayerNoEmblemsConsume` inside that block (1 = do not consume Spirit Emblems).
const PLAYER_NO_EMBLEMS_CONSUME: usize = 0x4;
/// The debug byte is static; checking it every frame is unnecessary. 1 s also picks up a live
/// ini change quickly enough (the settings file itself is polled every 2 s).
const RECHECK_MS: u64 = 1_000;

static ENABLED: AtomicBool = AtomicBool::new(true);
static LAST_APPLIED: AtomicBool = AtomicBool::new(false);
static NEXT_CHECK_MS: AtomicU64 = AtomicU64::new(0);

/// `unlimited_spirit_emblems` from the settings file (live).
pub fn set_enabled(on: bool) {
    ENABLED.store(on, Ordering::Relaxed);
}

fn flag_addr() -> usize {
    game::module_base() + DEBUG_FLAGS_RVA + PLAYER_NO_EMBLEMS_CONSUME
}

/// Keeps the game's debug byte in sync with the setting. Called once per frame.
pub fn tick() {
    let on = ENABLED.load(Ordering::Relaxed);
    let now = game::now_ms();
    if now < NEXT_CHECK_MS.load(Ordering::Relaxed) && LAST_APPLIED.load(Ordering::Relaxed) == on {
        return;
    }
    NEXT_CHECK_MS.store(now.saturating_add(RECHECK_MS), Ordering::Relaxed);

    let addr = flag_addr();
    let Some(current) = game::read::<u8>(addr) else { return };
    let want = u8::from(on);
    if current == want {
        if LAST_APPLIED.swap(on, Ordering::Relaxed) != on && on {
            log::line("emblems: unlimited spirit emblems on (PlayerNoEmblemsConsume = 1)");
        }
        return;
    }
    if game::write(addr, want) {
        LAST_APPLIED.store(on, Ordering::Relaxed);
        log::line(if on {
            "emblems: unlimited spirit emblems on (PlayerNoEmblemsConsume = 1)"
        } else {
            "emblems: unlimited spirit emblems off (PlayerNoEmblemsConsume = 0)"
        });
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The two constants are the documented SekiroTool values; a typo here would write into a
    /// random part of the exe's data, so keep them pinned in a test.
    #[test]
    fn debug_flag_offset_is_the_sekirotool_one() {
        assert_eq!(DEBUG_FLAGS_RVA + PLAYER_NO_EMBLEMS_CONSUME, 0x3D7A36A);
    }
}
