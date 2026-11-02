//! Repairs the vanilla Combat Art equipment slot (`流派招式`) staying locked.
//!
//! The slot is not unlocked merely by selecting a skill: the save also keeps two local
//! tutorial/menu flags (`6207` and `6290`; HANDOFF notes these opened the slot in an older
//! test). A join snapshot / own-world repair can leave those local flags off while the
//! skill itself is learned, so once any learned skill flag is on we re-light them.
//!
//! Only local writes: these flags are per player and never sent to the friend.

use std::sync::atomic::{AtomicU64, Ordering};

use fromsoftware_shared::FromStatic;
use sekiro::param::{SKILL_PARAM_ST, ParamDef};
use sekiro::sprj::SoloParamRepository;

use crate::{game, log, world};

/// `HANDOFF.md`: save edits with these two set opened the Combat Art slot.
const COMBAT_ART_SLOT_FLAGS: [u32; 2] = [6207, 6290];
const RECHECK_MS: u64 = 1_000;

static NEXT_CHECK_MS: AtomicU64 = AtomicU64::new(0);
static LOGGED: std::sync::atomic::AtomicBool = std::sync::atomic::AtomicBool::new(false);

fn any_skill_learned() -> bool {
    let Ok(repo) = (unsafe { SoloParamRepository::instance() }) else { return false };
    let Some(file) = repo.params().find(|p| p.struct_name() == SKILL_PARAM_ST::NAME).map(|p| &*p.data) else {
        return false;
    };
    unsafe { file.rows::<SKILL_PARAM_ST>() }
        .filter_map(|(_, r)| (r.unlock_event_flag() > 0).then(|| r.unlock_event_flag() as u32))
        .any(|flag| game::event_flag(flag) == Some(true))
}

/// Frame task: once per second, repair the vanilla slot flags after a skill is learned.
pub fn tick() {
    let now = game::now_ms();
    if now < NEXT_CHECK_MS.load(Ordering::Relaxed) {
        return;
    }
    NEXT_CHECK_MS.store(now.saturating_add(RECHECK_MS), Ordering::Relaxed);

    if !any_skill_learned() {
        return;
    }
    let mut changed = false;
    for id in COMBAT_ART_SLOT_FLAGS {
        if game::event_flag(id) != Some(true) {
            changed |= world::set_local_flag(id, true);
        }
    }
    if changed && !LOGGED.swap(true, Ordering::Relaxed) {
        log::line("skillslot: repaired the vanilla Combat Art slot flags");
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn slot_flags_are_the_documented_pair() {
        assert_eq!(COMBAT_ART_SLOT_FLAGS, [6207, 6290]);
    }
}
