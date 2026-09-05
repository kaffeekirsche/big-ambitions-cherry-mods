# Item-Assets anlegen (Kopie von FalconToy.asset → Ordner → umbenennen → Felder setzen)

Dateiname = Spalte "Datei", itemName = `cherryretail:itemname_<key>`, isADemandedProduct = an.

## Bakery/
| Datei | key | wholesale | market | boxSize |
|---|---|---|---|---|
| Bread | bread | 1.5 | 4.5 | 500 |
| BreadRoll | breadroll | 0.4 | 1.2 | 500 |
| Bagel | bagel | 0.6 | 1.8 | 500 |
| Croissant | croissant | 0.8 | 2.5 | 500 |
| Donut | donut | 0.7 | 2.2 | 500 |
| Pancake | pancake | 0.9 | 2.8 | 500 |
| Cake | cake | 6 | 18 | 500 |
| Cookies | cookies | 1.2 | 3.5 | 500 |
| Butter | butter | 1.0 | 2.8 | 500 |
| Sandwich | sandwich | 2.0 | 6 | 500 |

## PetFood/
| Datei | key | wholesale | market | boxSize |
|---|---|---|---|---|
| DogFood | dogfood | 6 | 18 | 500 |
| DogFoodPremium | dogfoodpremium | 14 | 42 | 500 |
| CatFood | catfood | 5 | 15 | 500 |
| CatFoodPremium | catfoodpremium | 12 | 36 | 500 |
| PetTreats | pettreats | 2.5 | 8 | 500 |
| CatLitter | catlitter | 4 | 12 | 500 |
| BirdSeed | birdseed | 2 | 7 | 500 |
| PetToy | pettoy | 3 | 9 | 500 |
| PetToyPremium | pettoypremium | 10 | 30 | 500 |
| LeashCollar | leashcollar | 8 | 25 | 500 |

## Pharmacy/
| Datei | key | wholesale | market | boxSize |
|---|---|---|---|---|
| ColdMedicine | coldmedicine | 3 | 9 | 500 |
| Painkiller | painkiller | 2.5 | 7.5 | 500 |
| Vitamins | vitamins | 4 | 12 | 500 |
| FirstAidKit | firstaidkit | 9 | 27 | 500 |
| Thermometer | thermometer | 5 | 15 | 500 |
| Sunscreen | sunscreen | 3.5 | 11 | 500 |

## BusinessType-Assets (Kopie von ToyStore.asset)
- Bakery/BakeryShop.asset → businessTypeName `cherryretail:businesstype_bakery`
- PetFood/PetFoodShop.asset → `cherryretail:businesstype_petfood`
- Pharmacy/PharmacyShop.asset → `cherryretail:businesstype_pharmacy`
- businessProducts: alle Keys des jeweiligen Ladens, impact 1
- icon: eigenes Sprite

## Noch offen
- Vanilla-Keys für Kaffee, Tee, Soda nachschlagen → als businessProducts der Bäckerei eintragen
