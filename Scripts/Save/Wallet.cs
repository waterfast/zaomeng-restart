using System;

namespace Zaomeng.Save;

public enum CurrencyType { Soul, Coupon }

/// <summary>整个存档共用的货币账户；本地双人角色访问同一个实例。</summary>
public sealed class Wallet
{
	public long Souls { get; set; }
	public long Coupons { get; set; }

	public long GetBalance(CurrencyType type) => type switch
	{
		CurrencyType.Soul => Souls,
		CurrencyType.Coupon => Coupons,
		_ => throw new ArgumentOutOfRangeException(nameof(type))
	};

	public void Add(CurrencyType type, long amount)
	{
		if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
		switch (type)
		{
			case CurrencyType.Soul: Souls = checked(Souls + amount); break;
			case CurrencyType.Coupon: Coupons = checked(Coupons + amount); break;
			default: throw new ArgumentOutOfRangeException(nameof(type));
		}
	}

	public bool TrySpend(CurrencyType type, long amount)
	{
		if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
		if (GetBalance(type) < amount) return false;
		switch (type)
		{
			case CurrencyType.Soul: Souls -= amount; break;
			case CurrencyType.Coupon: Coupons -= amount; break;
		}
		return true;
	}
}
