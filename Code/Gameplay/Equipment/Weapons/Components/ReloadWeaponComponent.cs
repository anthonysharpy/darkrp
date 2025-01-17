using Dxura.Darkrp;
using Sandbox.Events;

namespace Dxura.Darkrp;

[Title( "Reload" )]
[Group( "Weapon Components" )]
public partial class ReloadWeaponComponent : InputWeaponComponent,
	IGameEventHandler<EquipmentHolsteredEvent>
{
	/// <summary>
	/// How long does it take to reload?
	/// </summary>
	[Property]
	public float ReloadTime { get; set; } = 1.0f;

	/// <summary>
	/// How long does it take to reload while empty?
	/// </summary>
	[Property]
	public float EmptyReloadTime { get; set; } = 2.0f;

	[Property] public bool SingleReload { get; set; } = false;

	/// <summary>
	/// This is really just the magazine for the weapon. 
	/// </summary>
	[Property]
	public AmmoComponent AmmoComponent { get; set; }

	private TimeUntil TimeUntilReload { get; set; }
	[Sync] private bool IsReloading { get; set; }

	protected override void OnEnabled()
	{
		BindTag( "reloading", () => IsReloading );
	}

	protected override void OnInput()
	{
		if ( CanReload() )
		{
			StartReload();
		}
	}

	protected override void OnUpdate()
	{
		if ( !Player.IsValid() )
		{
			return;
		}

		if ( Player.IsProxy )
		{
			return;
		}

		if ( SingleReload && IsReloading && Input.Pressed( "Attack1" ) )
		{
			_queueCancel = true;
		}

		if ( IsReloading && TimeUntilReload )
		{
			EndReload();
		}
	}

	void IGameEventHandler<EquipmentHolsteredEvent>.OnGameEvent( EquipmentHolsteredEvent eventArgs )
	{
		if ( !IsProxy && IsReloading )
		{
			// Conna: if we're no longer deployed cancel reloading.
			CancelReload();
		}
	}

	private bool CanReload()
	{
		return !IsReloading && AmmoComponent.IsValid() && !AmmoComponent.IsFull;
	}

	private float GetReloadTime()
	{
		if ( !AmmoComponent.HasAmmo )
		{
			return EmptyReloadTime;
		}

		return ReloadTime;
	}

	private Dictionary<float, SoundEvent> GetReloadSounds()
	{
		if ( !AmmoComponent.HasAmmo )
		{
			return EmptyReloadSounds;
		}

		return TimedReloadSounds;
	}

	private bool _queueCancel = false;

	[Broadcast( NetPermission.OwnerOnly )]
	private void StartReload()
	{
		_queueCancel = false;

		var isContinuedReload = IsReloading;

		if ( !IsProxy )
		{
			IsReloading = true;
			TimeUntilReload = GetReloadTime();
		}

		if ( SingleReload )
		{
			// Tags will be better so we can just react to stimuli.
			Equipment.ViewModel?.ModelRenderer?.Set( "b_reloading", true );

			var hasAmmo = AmmoComponent.HasAmmo;
			Equipment.ViewModel?.ModelRenderer.Set( !hasAmmo ? "b_reloading_first_shell" : "b_reloading_shell", true );
		}
		else
		{
			// Tags will be better so we can just react to stimuli.
			Equipment.ViewModel?.ModelRenderer?.Set( "b_reload", true );
		}


		foreach ( var kv in GetReloadSounds() )
		{
			// Play this sound after a certain time but only if we're reloading.
			PlayAsyncSound( kv.Key, kv.Value, () => IsReloading );
		}

		Equipment.Owner?.BodyRenderer?.Set( "b_reload", true );
	}

	[Broadcast( NetPermission.OwnerOnly )]
	private void CancelReload()
	{
		if ( !IsProxy )
		{
			IsReloading = false;
		}

		// Tags will be better so we can just react to stimuli.
		Equipment.ViewModel?.ModelRenderer?.Set( "b_reload", false );
		Equipment.Owner?.BodyRenderer?.Set( "b_reload", false );
		Equipment.ViewModel?.ModelRenderer?.Set( "b_reloading", false );
	}

	[Broadcast( NetPermission.OwnerOnly )]
	private void EndReload()
	{
		if ( !IsProxy )
		{
			if ( SingleReload )
			{
				AmmoComponent.Ammo++;
				AmmoComponent.Ammo = AmmoComponent.Ammo.Clamp( 0, AmmoComponent.MaxAmmo );

				// Reload more!
				if ( !_queueCancel && AmmoComponent.Ammo < AmmoComponent.MaxAmmo )
				{
					StartReload();
				}
				else
				{
					Equipment.ViewModel?.ModelRenderer?.Set( "b_reloading", false );
					IsReloading = false;
				}
			}
			else
			{
				IsReloading = false;
				// Refill the ammo container.
				AmmoComponent.Ammo = AmmoComponent.MaxAmmo;
			}
		}

		// Tags will be better so we can just react to stimuli.
		Equipment.ViewModel?.ModelRenderer.Set( "b_reload", false );
	}

	[Property] public Dictionary<float, SoundEvent> TimedReloadSounds { get; set; } = new();
	[Property] public Dictionary<float, SoundEvent> EmptyReloadSounds { get; set; } = new();

	private async void PlayAsyncSound( float delay, SoundEvent snd, Func<bool> playCondition = null )
	{
		await GameTask.DelaySeconds( delay );

		// Can we play this sound?
		if ( playCondition != null && !playCondition.Invoke() )
		{
			return;
		}

		GameObject.PlaySound( snd );
	}
}
