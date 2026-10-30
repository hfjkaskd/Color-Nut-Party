from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[3]
LANGUAGES = ('en', 'ja', 'pt', 'de', 'fr', 'id', 'ru', 'es')
# Source text is an exact lookup key. Translations favor short, natural UI wording.
# Pipes are column separators; literal \\n is converted to a line break.
ROWS = r'''
Withdrawal on Clear|クリアで出金|Saque ao concluir|Auszahlung bei Abschluss|Retrait après réussite|Tarik setelah selesai|Вывод после победы|Retiro al completar
Withdraw|出金|Sacar|Auszahlen|Retirer|Tarik|Вывести|Retirar
View your cash reward|現金報酬を見る|Ver recompensa em dinheiro|Geldbelohnung ansehen|Voir la récompense|Lihat hadiah uang|Посмотреть награду|Ver recompensa
Withdrawal History|出金履歴|Histórico de saques|Auszahlungsverlauf|Historique des retraits|Riwayat penarikan|История вывода|Historial de retiros
View withdrawal history|出金履歴を見る|Ver histórico de saques|Auszahlungsverlauf öffnen|Voir les retraits|Lihat riwayat penarikan|Открыть историю вывода|Ver historial de retiros
Cash Reward #{0}|現金報酬 #{0}|Recompensa #{0}|Geldbelohnung #{0}|Récompense #{0}|Hadiah uang #{0}|Денежная награда №{0}|Recompensa #{0}
Pending - not paid|保留中・未払い|Pendente · não pago|Ausstehend · unbezahlt|En attente · non payé|Tertunda · belum dibayar|Ожидает · не выплачено|Pendiente · sin pagar
This reward could not be moved to history. Please review your cash balance.|報酬を履歴に移せませんでした。現金残高をご確認ください。|Não foi possível salvar a recompensa no histórico. Confira seu saldo.|Die Belohnung konnte nicht im Verlauf gespeichert werden. Prüfe dein Guthaben.|Impossible d'ajouter la récompense à l'historique. Vérifiez votre solde.|Hadiah gagal dipindahkan ke riwayat. Periksa saldo uang Anda.|Не удалось сохранить награду в истории. Проверьте баланс.|No se pudo guardar la recompensa en el historial. Revisa tu saldo.
Reward assessment|報酬を確認中|Analisando recompensas|Belohnung wird geprüft|Évaluation des récompenses|Memeriksa hadiah|Проверка наград|Evaluando recompensas
Your first two cash rewards are saved in withdrawal history.|最初の2つの現金報酬を出金履歴に保存しました。|Suas duas primeiras recompensas foram salvas no histórico de saques.|Deine ersten zwei Geldbelohnungen sind im Auszahlungsverlauf gespeichert.|Vos deux premières récompenses sont enregistrées dans l'historique des retraits.|Dua hadiah uang pertama Anda tersimpan di riwayat penarikan.|Первые две денежные награды сохранены в истории вывода.|Tus dos primeras recompensas se guardaron en el historial de retiros.
Activating rewards|報酬を有効化中|Ativando recompensas|Belohnungen aktivieren|Activation des récompenses|Mengaktifkan hadiah|Активация наград|Activando recompensas
Preparing your cash rewards and level bonuses...|現金報酬とレベルボーナスを準備中…|Preparando suas recompensas e bônus de nível...|Geldbelohnungen und Levelboni werden vorbereitet...|Préparation des récompenses et bonus de niveau…|Menyiapkan hadiah uang dan bonus level...|Готовим денежные награды и бонусы уровней…|Preparando tus recompensas y bonos de nivel...
Your reward bonuses|報酬ボーナス|Seus bônus|Deine Belohnungsboni|Vos bonus|Bonus hadiah Anda|Ваши бонусы|Tus bonos
Extra cash reward|追加の現金報酬|Recompensa extra|Zusätzliche Geldbelohnung|Récompense supplémentaire|Hadiah uang tambahan|Дополнительная награда|Recompensa extra
Clear a rod to earn a reward|ボルトを完成させて報酬獲得|Complete um parafuso e ganhe|Schließe einen Bolzen ab|Terminez une tige pour gagner|Selesaikan baut untuk hadiah|Соберите болт для награды|Completa un perno y gana
Watch a reward video for 10X|動画を見て報酬10倍|Veja um vídeo para ganhar 10X|Video ansehen für 10X|Regardez une vidéo pour 10X|Tonton video untuk 10X|Видео: награда ×10|Mira un vídeo para ganar 10X
Unlock diamond rewards|ダイヤ報酬を解放|Libere recompensas de diamantes|Diamantbelohnungen freischalten|Débloquez les diamants|Buka hadiah berlian|Откройте награды алмазами|Desbloquea diamantes
Level 5 withdrawal requirements|レベル5の出金条件|Requisitos de saque do nível 5|Auszahlungsregeln für Level 5|Conditions de retrait du niveau 5|Syarat penarikan level 5|Условия вывода на уровне 5|Requisitos de retiro del nivel 5
Rewards activated|報酬の\n有効化完了|Recompensas ativadas|Belohnungen aktiviert|Récompenses activées|Hadiah diaktifkan|Награды активированы|Recompensas activadas
Continue playing to collect cash and unlock more rewards.|プレイを続けて現金を集め、さらに報酬を解放しましょう。|Continue jogando para juntar dinheiro e liberar mais recompensas.|Spiele weiter, sammle Geld und schalte mehr Belohnungen frei.|Continuez à jouer pour collecter de l'argent et débloquer des récompenses.|Terus bermain untuk mengumpulkan uang dan membuka hadiah lainnya.|Играйте дальше, собирайте деньги и открывайте новые награды.|Sigue jugando para reunir dinero y desbloquear más recompensas.
Your withdrawal history|あなたの出金履歴|Seu histórico de saques|Dein Auszahlungsverlauf|Votre historique des retraits|Riwayat penarikan Anda|Ваша история вывода|Tu historial de retiros
Your reward records are saved. Submitted requests remain pending; no payment has been made.|報酬記録を保存しました。申請は保留中です。支払いは行われていません。|Registros salvos. Os pedidos enviados estão pendentes; nenhum pagamento foi feito.|Belohnungen gespeichert. Anträge bleiben ausstehend; es wurde nichts ausgezahlt.|Récompenses enregistrées. Les demandes restent en attente ; aucun paiement effectué.|Catatan hadiah tersimpan. Permintaan tetap tertunda; belum ada pembayaran.|Награды сохранены. Заявки ожидают обработки; выплат не было.|Recompensas guardadas. Las solicitudes siguen pendientes; no se ha realizado ningún pago.
Claim {0}X|{0}倍で受け取る|Receber {0}X|{0}X abholen|Recevoir {0}X|Klaim {0}X|Забрать ×{0}|Recibir {0}X
Lucky Reward|ラッキー報酬|Recompensa da sorte|Glücksbelohnung|Récompense chanceuse|Hadiah keberuntungan|Счастливая награда|Premio de la suerte
Extra reward unlocked|追加報酬を解放|Recompensa extra liberada|Zusatzbelohnung freigeschaltet|Bonus débloqué|Hadiah tambahan terbuka|Бонусная награда открыта|Premio extra desbloqueado
Clear this level to claim extra reward\n{0} diamonds|このレベルをクリアして追加報酬\nダイヤ{0}個を獲得|Conclua este nível para receber\n{0} diamantes extras|Schließe das Level ab für\n{0} zusätzliche Diamanten|Terminez ce niveau pour gagner\n{0} diamants bonus|Selesaikan level ini untuk mendapat\n{0} berlian tambahan|Пройдите уровень и получите\nещё {0} алмазов|Completa este nivel para recibir\n{0} diamantes extra
Diamond rewards are unlocked. Tap here to review withdrawal requirements.|ダイヤ報酬を解放しました。ここをタップして出金条件を確認。|Recompensas de diamantes liberadas. Toque aqui para ver os requisitos de saque.|Diamantbelohnungen freigeschaltet. Tippe hier für die Auszahlungsregeln.|Diamants débloqués. Touchez ici pour voir les conditions de retrait.|Hadiah berlian terbuka. Ketuk di sini untuk melihat syarat penarikan.|Награды алмазами открыты. Нажмите здесь, чтобы узнать условия вывода.|Diamantes desbloqueados. Toca aquí para ver los requisitos de retiro.
Reward Received|報酬を獲得|Recompensa recebida|Belohnung erhalten|Récompense reçue|Hadiah diterima|Награда получена|Recompensa recibida
Continue|続ける|Continuar|Weiter|Continuer|Lanjut|Продолжить|Continuar
Withdrawal request|出金申請|Pedido de saque|Auszahlungsantrag|Demande de retrait|Permintaan penarikan|Заявка на вывод|Solicitud de retiro
Reward #{0}|報酬 #{0}|Recompensa #{0}|Belohnung #{0}|Récompense #{0}|Hadiah #{0}|Награда №{0}|Recompensa #{0}
Confirm withdrawal|出金を確定|Confirmar saque|Auszahlung bestätigen|Confirmer le retrait|Konfirmasi penarikan|Подтвердить вывод|Confirmar retiro
Current status|現在の状況|Status atual|Aktueller Status|Statut actuel|Status saat ini|Текущий статус|Estado actual
Reward recorded|報酬を記録済み|Recompensa registrada|Belohnung gespeichert|Récompense enregistrée|Hadiah tercatat|Награда записана|Recompensa registrada
Account details entered|口座情報を入力済み|Dados da conta inseridos|Kontodaten eingegeben|Coordonnées saisies|Data akun diisi|Данные счёта введены|Datos de cuenta ingresados
Ready to submit|申請の準備完了|Pronto para enviar|Bereit zum Absenden|Prêt à envoyer|Siap dikirim|Готово к отправке|Listo para enviar
Withdrawal requirements|出金条件|Requisitos de saque|Auszahlungsbedingungen|Conditions de retrait|Syarat penarikan|Условия вывода|Requisitos de retiro
Cash withdrawal|現金の出金|Saque em dinheiro|Geld auszahlen|Retrait d'argent|Penarikan uang|Вывод денег|Retiro de dinero
Diamond withdrawal|ダイヤの出金|Saque de diamantes|Diamanten auszahlen|Retrait de diamants|Penarikan berlian|Вывод алмазов|Retiro de diamantes
Complete level {0}|レベル{0}をクリア|Conclua o nível {0}|Level {0} abschließen|Terminez le niveau {0}|Selesaikan level {0}|Пройдите уровень {0}|Completa el nivel {0}
Required balance|必要残高|Saldo necessário|Benötigtes Guthaben|Solde requis|Saldo yang diperlukan|Нужный баланс|Saldo requerido
Complete cash guidance|現金ガイドを完了|Conclua o guia de dinheiro|Geldanleitung abschließen|Terminez le guide d'argent|Selesaikan panduan uang|Завершите обучение|Completa la guía de dinero
Levels completed today|今日のクリア数|Níveis concluídos hoje|Heute geschaffte Level|Niveaux réussis aujourd'hui|Level selesai hari ini|Уровней пройдено сегодня|Niveles completados hoy
Login days|ログイン日数|Dias de login|Anmeldetage|Jours de connexion|Hari masuk|Дней входа|Días de inicio de sesión
Account level|アカウントレベル|Nível da conta|Kontolevel|Niveau du compte|Level akun|Уровень аккаунта|Nivel de cuenta
Awaiting account update|アカウント更新待ち|Aguardando atualização|Kontoaktualisierung ausstehend|Mise à jour en attente|Menunggu pembaruan akun|Ожидание обновления|Esperando actualización
Complete|完了|Concluído|Abgeschlossen|Terminé|Selesai|Готово|Completado
Login complete|ログイン完了|Login concluído|Angemeldet|Connexion terminée|Sudah masuk|Вход выполнен|Sesión iniciada
In progress|進行中|Em andamento|In Arbeit|En cours|Berlangsung|В процессе|En curso
Continue playing|プレイを続ける|Continuar jogando|Weiterspielen|Continuer à jouer|Lanjut bermain|Играть дальше|Seguir jugando
View next stage|次の段階を見る|Ver próxima etapa|Nächste Stufe ansehen|Voir l'étape suivante|Lihat tahap berikutnya|Следующий этап|Ver siguiente etapa
Back to withdrawal|出金に戻る|Voltar ao saque|Zurück zur Auszahlung|Retour au retrait|Kembali ke penarikan|Назад к выводу|Volver al retiro
Requirements review|条件を確認|Revisão dos requisitos|Bedingungen prüfen|Vérification des conditions|Tinjau persyaratan|Проверка условий|Revisar requisitos
Complete the next level target to continue. Your current progress is shown below.|次のレベル目標を達成して続けましょう。現在の進捗は下に表示されます。|Conclua a próxima meta de nível para continuar. Veja seu progresso abaixo.|Erreiche das nächste Levelziel. Dein aktueller Fortschritt steht unten.|Atteignez le prochain objectif de niveau pour continuer. Votre progression est ci-dessous.|Capai target level berikutnya untuk lanjut. Progres saat ini ditampilkan di bawah.|Достигните следующей цели уровня. Текущий прогресс показан ниже.|Alcanza la siguiente meta de nivel para continuar. Tu progreso aparece abajo.
Withdrawal progress|出金の進捗|Progresso do saque|Auszahlungsfortschritt|Progression du retrait|Progres penarikan|Прогресс вывода|Progreso del retiro
Complete the next stage requirements|次の段階の条件を達成|Cumpra os requisitos da próxima etapa|Erfülle die nächste Stufe|Remplissez les conditions suivantes|Penuhi syarat tahap berikutnya|Выполните условия следующего этапа|Cumple los requisitos de la siguiente etapa
First level target completed|最初のレベル目標を達成|Primeira meta concluída|Erstes Levelziel erreicht|Premier objectif atteint|Target level pertama tercapai|Первая цель достигнута|Primera meta completada
Next level target pending|次のレベル目標は未達成|Próxima meta pendente|Nächstes Levelziel offen|Prochain objectif en attente|Target level berikutnya tertunda|Следующая цель не достигнута|Siguiente meta pendiente
Review remaining requirements before applying|申請前に残りの条件を確認|Confira os requisitos antes de solicitar|Prüfe offene Bedingungen vor dem Antrag|Vérifiez les conditions avant la demande|Periksa syarat tersisa sebelum mengajukan|Проверьте условия перед заявкой|Revisa los requisitos antes de solicitar
Complete level <color=#FF0000>{0}</color> to continue the next stage|レベル<color=#FF0000>{0}</color>をクリアして次の段階へ|Conclua o nível <color=#FF0000>{0}</color> para seguir à próxima etapa|Schließe Level <color=#FF0000>{0}</color> für die nächste Stufe ab|Terminez le niveau <color=#FF0000>{0}</color> pour passer à l'étape suivante|Selesaikan level <color=#FF0000>{0}</color> untuk lanjut ke tahap berikutnya|Пройдите уровень <color=#FF0000>{0}</color> для перехода дальше|Completa el nivel <color=#FF0000>{0}</color> para pasar a la siguiente etapa
Application not submitted|申請は未送信です|Pedido não enviado|Antrag nicht gesendet|Demande non envoyée|Permintaan belum dikirim|Заявка не отправлена|Solicitud no enviada
Reward videos watched|視聴済み報酬動画|Vídeos de recompensa vistos|Angesehene Belohnungsvideos|Vidéos récompensées vues|Video hadiah ditonton|Просмотрено видео с наградой|Vídeos de recompensa vistos
Enter payment details|支払い情報を入力|Inserir dados de pagamento|Zahlungsdaten eingeben|Saisir les coordonnées|Masukkan data pembayaran|Введите платёжные данные|Ingresa los datos de pago
Requirements complete. Confirm your payment details.|条件達成。支払い情報を確認してください。|Requisitos cumpridos. Confirme seus dados de pagamento.|Bedingungen erfüllt. Bestätige deine Zahlungsdaten.|Conditions remplies. Confirmez vos coordonnées de paiement.|Syarat terpenuhi. Konfirmasi data pembayaran Anda.|Условия выполнены. Подтвердите платёжные данные.|Requisitos cumplidos. Confirma tus datos de pago.
Complete the requirements below to continue.|下記の条件を達成して続けましょう。|Cumpra os requisitos abaixo para continuar.|Erfülle die Bedingungen unten, um fortzufahren.|Remplissez les conditions ci-dessous pour continuer.|Penuhi syarat di bawah untuk melanjutkan.|Выполните условия ниже, чтобы продолжить.|Cumple los requisitos de abajo para continuar.
Withdrawal service is not connected. No withdrawal has been submitted.|出金サービスに未接続です。出金申請は送信されていません。|O serviço de saque não está conectado. Nenhum pedido foi enviado.|Der Auszahlungsdienst ist nicht verbunden. Es wurde kein Antrag gesendet.|Le service de retrait n'est pas connecté. Aucune demande n'a été envoyée.|Layanan penarikan belum terhubung. Tidak ada permintaan yang dikirim.|Сервис вывода не подключён. Заявка на вывод не отправлена.|El servicio de retiros no está conectado. No se ha enviado ninguna solicitud.
Settings|設定|Configurações|Einstellungen|Paramètres|Pengaturan|Настройки|Ajustes
Sound|サウンド|Som|Ton|Son|Suara|Звук|Sonido
Music & effects|音楽と効果音|Música e efeitos|Musik und Effekte|Musique et effets|Musik dan efek|Музыка и эффекты|Música y efectos
Vibration|振動|Vibração|Vibration|Vibration|Getaran|Вибрация|Vibración
ON|オン|SIM|AN|OUI|AKTIF|ВКЛ|SÍ
OFF|オフ|NÃO|AUS|NON|NONAKTIF|ВЫКЛ|NO
Id:{0}|ID:{0}|ID:{0}|ID:{0}|ID:{0}|ID:{0}|ID:{0}|ID:{0}
LV.{0}|Lv.{0}|Nv.{0}|Lv.{0}|Nv.{0}|Lv.{0}|Ур.{0}|Nv.{0}
Current Level: {0}|現在のレベル：{0}|Nível atual: {0}|Aktuelles Level: {0}|Niveau actuel : {0}|Level saat ini: {0}|Текущий уровень: {0}|Nivel actual: {0}
Progress unavailable|進捗は取得できません|Progresso indisponível|Fortschritt nicht verfügbar|Progression indisponible|Progres tidak tersedia|Прогресс недоступен|Progreso no disponible
Simulated stats ({0})|シミュレーション値（{0}）|Dados simulados ({0})|Simulierte Werte ({0})|Données simulées ({0})|Data simulasi ({0})|Симуляция данных ({0})|Datos simulados ({0})
Withdrawal records unavailable|出金記録は取得できません|Registros de saque indisponíveis|Auszahlungsdaten nicht verfügbar|Historique indisponible|Catatan penarikan tidak tersedia|Записи о выводе недоступны|Registros de retiro no disponibles
Select a withdrawal method and enter the matching account details.|出金方法を選び、対応する口座情報を入力してください。|Escolha a forma de saque e informe os dados da conta correspondente.|Wähle eine Auszahlungsmethode und gib die passenden Kontodaten an.|Choisissez un mode de retrait et saisissez les coordonnées du compte associé.|Pilih metode penarikan dan masukkan data akun yang sesuai.|Выберите способ вывода и укажите данные соответствующего счёта.|Elige el método de retiro e ingresa los datos de la cuenta correspondiente.
Interstitial ad completed|広告の再生完了|Anúncio concluído|Werbung abgeschlossen|Publicité terminée|Iklan selesai|Реклама завершена|Anuncio completado
Complete this level to continue in local mode|このレベルをクリアしてローカルモードで続ける|Conclua este nível para continuar no modo local|Schließe dieses Level ab, um lokal fortzufahren|Terminez ce niveau pour continuer en mode local|Selesaikan level ini untuk lanjut dalam mode lokal|Пройдите уровень для продолжения в локальном режиме|Completa este nivel para seguir en modo local
Reward received|報酬を獲得|Recompensa recebida|Belohnung erhalten|Récompense reçue|Hadiah diterima|Награда получена|Recompensa recibida
Ad unavailable. Please try again.|広告を利用できません。もう一度お試しください。|Anúncio indisponível. Tente novamente.|Werbung nicht verfügbar. Versuche es erneut.|Publicité indisponible. Réessayez.|Iklan tidak tersedia. Coba lagi.|Реклама недоступна. Попробуйте снова.|Anuncio no disponible. Inténtalo de nuevo.
Withdrawal service is not connected.|出金サービスに未接続です。|O serviço de saque não está conectado.|Der Auszahlungsdienst ist nicht verbunden.|Le service de retrait n'est pas connecté.|Layanan penarikan belum terhubung.|Сервис вывода не подключён.|El servicio de retiros no está conectado.
Current cash balance|現在の現金残高|Saldo atual|Aktuelles Guthaben|Solde actuel|Saldo uang saat ini|Текущий баланс|Saldo actual
Replay|やり直す|Rejogar|Neu spielen|Rejouer|Ulangi|Переиграть|Repetir
User Level|プレイヤーレベル|Nível do jogador|Spielerlevel|Niveau du joueur|Level pemain|Уровень игрока|Nivel del jugador
<u>User Level</u>|<u>プレイヤーレベル</u>|<u>Nível do jogador</u>|<u>Spielerlevel</u>|<u>Niveau du joueur</u>|<u>Level pemain</u>|<u>Уровень игрока</u>|<u>Nivel del jugador</u>
Level progress|レベル進捗|Progresso do nível|Levelfortschritt|Progression du niveau|Progres level|Прогресс уровня|Progreso del nivel
Player|プレイヤー|Jogador|Spieler|Joueur|Pemain|Игрок|Jugador
You|あなた|Você|Du|Vous|Anda|Вы|Tú
Bonus 1|ボーナス1|Bônus 1|Bonus 1|Bonus 1|Bonus 1|Бонус 1|Bono 1
Bonus 2|ボーナス2|Bônus 2|Bonus 2|Bonus 2|Bonus 2|Бонус 2|Bono 2
Bonus 3|ボーナス3|Bônus 3|Bonus 3|Bonus 3|Bonus 3|Бонус 3|Bono 3
Bonus 4|ボーナス4|Bônus 4|Bonus 4|Bonus 4|Bonus 4|Бонус 4|Bono 4
Bonus 5|ボーナス5|Bônus 5|Bonus 5|Bonus 5|Bonus 5|Бонус 5|Bono 5
Bonus {0}|ボーナス{0}|Bônus {0}|Bonus {0}|Bonus {0}|Bonus {0}|Бонус {0}|Bono {0}
Reward benefit|報酬ボーナス|Bônus de recompensa|Belohnungsbonus|Bonus de récompense|Bonus hadiah|Бонус награды|Bono de recompensa
Select Screw To Shuffle|シャッフルするボルトを選択|Escolha um parafuso para misturar|Bolzen zum Mischen wählen|Choisissez une tige à mélanger|Pilih baut untuk diacak|Выберите болт для перемешивания|Elige un perno para mezclar
Level complete|レベルクリア|Nível concluído|Level geschafft|Niveau terminé|Level selesai|Уровень пройден|Nivel completado
Level Complete|レベルクリア|Nível concluído|Level geschafft|Niveau terminé|Level selesai|Уровень пройден|Nivel completado
Cash|出金|Sacar|Auszahlen|Retirer|Tarik|Вывод|Retirar
Claim|受け取る|Receber|Abholen|Recevoir|Klaim|Забрать|Recibir
Claim 10X|10倍で受け取る|Receber 10X|10X abholen|Recevoir 10X|Klaim 10X|Забрать ×10|Recibir 10X
Confirm|確認|Confirmar|Bestätigen|Confirmer|Konfirmasi|Подтвердить|Confirmar
Cancel|キャンセル|Cancelar|Abbrechen|Annuler|Batal|Отмена|Cancelar
Close|閉じる|Fechar|Schließen|Fermer|Tutup|Закрыть|Cerrar
Loading...|読み込み中…|Carregando...|Wird geladen...|Chargement…|Memuat...|Загрузка…|Cargando...
LOADING...|読み込み中…|CARREGANDO...|WIRD GELADEN...|CHARGEMENT…|MEMUAT...|ЗАГРУЗКА…|CARGANDO...
Withdraw All|すべて出金|Sacar tudo|Alles auszahlen|Tout retirer|Tarik semua|Вывести всё|Retirar todo
Enter Information|情報を入力|Inserir informações|Angaben eingeben|Saisir les informations|Masukkan informasi|Введите данные|Ingresa información
Enter Name|名前を入力|Digite o nome|Namen eingeben|Saisir le nom|Masukkan nama|Введите имя|Ingresa tu nombre
Enter phone number|電話番号を入力|Digite o telefone|Telefonnummer eingeben|Saisir le téléphone|Masukkan nomor telepon|Введите номер телефона|Ingresa tu teléfono
Enter email address|メールアドレスを入力|Digite o e-mail|E-Mail-Adresse eingeben|Saisir l'adresse e-mail|Masukkan alamat email|Введите адрес эл. почты|Ingresa tu correo
Select a withdrawal method|出金方法を選択|Escolha a forma de saque|Auszahlungsmethode wählen|Choisissez un mode de retrait|Pilih metode penarikan|Выберите способ вывода|Elige el método de retiro
Players cleared today|今日のクリア人数|Jogadores que passaram hoje|Spieler mit Erfolg heute|Joueurs ayant réussi aujourd'hui|Pemain lolos hari ini|Игроков прошли сегодня|Jugadores que pasaron hoy
Average attempts|平均挑戦回数|Média de tentativas|Versuche im Schnitt|Essais moyens|Rata-rata percobaan|Среднее число попыток|Intentos promedio
Average withdrawal amount|平均出金額|Saque médio|Auszahlung im Schnitt|Retrait moyen|Rata-rata penarikan|Средняя сумма вывода|Retiro promedio
Bolt+|ボルト追加|Parafuso+|Bolzen+|Tige+|Baut+|Болт+|Perno+
Add an empty bolt|空のボルトを追加|Adicionar um parafuso vazio|Leeren Bolzen hinzufügen|Ajouter une tige vide|Tambah baut kosong|Добавить пустой болт|Añadir un perno vacío
Refresh nut arrangement|ナットの配置を変更|Misturar as porcas|Muttern neu anordnen|Mélanger les écrous|Acak susunan mur|Перемешать гайки|Mezclar las tuercas
Welcome|ようこそ|Boas-vindas|Willkommen|Bienvenue|Selamat datang|Добро пожаловать|Bienvenido
Hello|こんにちは|Olá|Hallo|Bonjour|Halo|Здравствуйте|Hola
Dedicated Support\nis assisting you|専任サポートが\nお手伝いします|O suporte dedicado\nestá ajudando você|Unser Support\nhilft dir weiter|L'assistance dédiée\nvous accompagne|Dukungan khusus\nsiap membantu Anda|Вам помогает\nслужба поддержки|El soporte dedicado\nte está ayudando
Support ID: {0}|サポートID：{0}|ID de suporte: {0}|Support-ID: {0}|ID d'assistance : {0}|ID Dukungan: {0}|ID поддержки: {0}|ID de soporte: {0}
Local|ローカル|Local|Lokal|Local|Lokal|Локально|Local
LOCAL MODE - SDK skipped|ローカルモード・SDK未使用|MODO LOCAL - sem SDK|LOKALER MODUS - ohne SDK|MODE LOCAL - sans SDK|MODE LOKAL - SDK dilewati|ЛОКАЛЬНЫЙ РЕЖИМ — без SDK|MODO LOCAL - sin SDK
Tool unavailable\nSDK rewards are skipped|道具を利用できません\nSDK報酬は無効です|Item indisponível\nRecompensas do SDK desativadas|Hilfsmittel nicht verfügbar\nSDK-Belohnungen deaktiviert|Objet indisponible\nRécompenses SDK désactivées|Alat tidak tersedia\nHadiah SDK dilewati|Предмет недоступен\nНаграды SDK отключены|Objeto no disponible\nRecompensas del SDK desactivadas
Account...|口座…|Conta...|Konto...|Compte…|Akun...|Счёт…|Cuenta...
Enter name...|名前を入力…|Digite o nome...|Namen eingeben...|Saisir le nom…|Masukkan nama...|Введите имя…|Ingresa tu nombre...
Enter text...|入力…|Digite aqui...|Hier eingeben...|Saisir le texte…|Masukkan teks...|Введите текст…|Escribe aquí...
Phone/Email|電話／メール|Telefone/E-mail|Telefon/E-Mail|Téléphone/E-mail|Telepon/Email|Телефон/Эл. почта|Teléfono/Correo
Dont move Nut!|ナットを動かさないで！|Não mova a porca!|Mutter nicht bewegen!|Ne bougez pas l'écrou !|Jangan pindahkan mur!|Не двигайте гайку!|¡No muevas la tuerca!
I know|わかりました|Entendi|Verstanden|Compris|Mengerti|Понятно|Entendido
My Diamond|所持ダイヤ|Meus diamantes|Meine Diamanten|Mes diamants|Berlian saya|Мои алмазы|Mis diamantes
My Level|自分のレベル|Meu nível|Mein Level|Mon niveau|Level saya|Мой уровень|Mi nivel
Only a little|少しだけ|Só um pouco|Nur ein wenig|Juste un peu|Sedikit saja|Совсем немного|Solo un poco
X10 Times Claimed|10倍報酬を獲得済み|Recompensa 10X recebida|10X-Belohnung abgeholt|Récompense 10X reçue|Hadiah 10X diklaim|Награда ×10 получена|Recompensa 10X recibida
Withdrawal goals completed|出金条件を達成|Metas de saque cumpridas|Auszahlungsziele erreicht|Objectifs de retrait atteints|Target penarikan tercapai|Цели вывода достигнуты|Metas de retiro cumplidas
Revive|復活|Reviver|Fortsetzen|Reprendre|Lanjutkan|Возродиться|Revivir
Revoke|元に戻す|Desfazer|Rückgängig|Annuler|Urungkan|Отменить ход|Deshacer
Restart|やり直す|Reiniciar|Neustart|Recommencer|Mulai ulang|Заново|Reiniciar
Restarting will lose your progress on this level\nAre you sure you want to do this?|やり直すと、このレベルの進捗が失われます。\nよろしいですか？|Reiniciar apagará seu progresso neste nível.\nTem certeza?|Beim Neustart geht dein Levelfortschritt verloren.\nBist du sicher?|Recommencer effacera votre progression dans ce niveau.\nVoulez-vous continuer ?|Memulai ulang akan menghapus progres level ini.\nAnda yakin?|При перезапуске прогресс уровня будет потерян.\nВы уверены?|Al reiniciar perderás tu progreso en este nivel.\n¿Quieres continuar?
OK|OK|OK|OK|OK|OK|ОК|OK
MAX|最大|MÁX.|MAX.|MAX.|MAKS.|МАКС.|MÁX.
Current State|現在の状況|Status atual|Aktueller Status|Statut actuel|Status saat ini|Текущий статус|Estado actual
Success|成功|Sucesso|Geschafft|Réussite|Berhasil|Успех|Éxito
Tip|ヒント|Dica|Tipp|Conseil|Tips|Подсказка|Consejo
Sure|確認|Confirmar|Bestätigen|Confirmer|Konfirmasi|Подтвердить|Confirmar
Level {0}|レベル{0}|Nível {0}|Level {0}|Niveau {0}|Level {0}|Уровень {0}|Nivel {0}
Level Stats ({0})|レベル統計（{0}）|Estatísticas ({0})|Levelstatistik ({0})|Statistiques ({0})|Statistik level ({0})|Статистика ({0})|Estadísticas ({0})
Withdrawal amount|出金額|Valor do saque|Auszahlungsbetrag|Montant du retrait|Jumlah penarikan|Сумма вывода|Importe del retiro
'''

def main():
    data = {}
    for line in ROWS.strip().splitlines():
        columns = line.split('|')
        if len(columns) != len(LANGUAGES):
            raise ValueError(f'Expected 8 languages: {columns[0]} ({len(columns)})')
        values = [value.replace(r'\n', '\n') for value in columns]
        english = values[0]
        if english in data:
            raise ValueError(f'Duplicate source: {english}')
        expected = sorted(re.findall(r'\{[^{}]+\}', english))
        expected_tags = re.findall(r'<[^>]+>', english)
        for language, value in zip(LANGUAGES, values):
            if not value or sorted(re.findall(r'\{[^{}]+\}', value)) != expected:
                raise ValueError(f'Placeholder mismatch: {language} {english}')
            if re.findall(r'<[^>]+>', value) != expected_tags:
                raise ValueError(f'Rich-text mismatch: {language} {english}')
        data[english] = dict(zip(LANGUAGES, values))
    # The settings button serializes its English source with a manual line break.
    # Other languages wrap naturally inside the same prefab label.
    data['Withdrawal\nHistory'] = dict(data['Withdrawal History'])
    data['Withdrawal\nHistory']['en'] = 'Withdrawal\nHistory'
    source_path = Path(__file__).parent / 'interface-source-keys.json'
    source_keys = json.loads(source_path.read_text(encoding='utf-8'))
    missing = set(source_keys) - set(data)
    if missing:
        raise ValueError('Missing configured text: ' + repr(sorted(missing)))
    output = ROOT / 'Assets/Resources/Configuration/Localization/InterfaceText.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    audit = {
        'sourceCount': len(data),
        'languages': list(LANGUAGES),
        'translationCount': len(data) * len(LANGUAGES),
        'configuredDisplayStringCount': len(source_keys),
        'missingConfiguredDisplayStrings': [],
        'placeholderAndRichTextValidation': 'passed',
        'runtimeScope': 'All three local presentation configuration files, settings, user level, simulated statistics, feedback and common static labels.',
        'excluded': ['Resource paths', 'Policy/source identifiers', 'Diagnostics and exception messages', 'Player-entered names and account details', 'Color Nut Party and payment-provider brand names'],
        'visualAcceptance': 'Pending Unity matrix and device verification; resource checks do not prove layout correctness.',
        'sourceKeys': list(data),
    }
    (Path(__file__).parent / 'interface-translation-audit.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'{len(data)} source strings, {len(data) * len(LANGUAGES)} translations: {output}')

if __name__ == '__main__':
    main()
